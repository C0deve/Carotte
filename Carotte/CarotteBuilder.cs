using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Carotte.Exceptions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Carotte;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarotte(this IServiceCollection services, Action<CarotteBuilder> configure)
    {
        return services.AddCarotteCore((CarotteOptions?)null, configure);
    }

    public static IServiceCollection AddCarotte(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CarotteBuilder>? configure = null)
    {
        var options = new CarotteOptions();
        configuration.Bind(options);

        services.Configure<CarotteOptions>(configuration);

        return services.AddCarotteCore(options, configure);
    }

    public static IServiceCollection AddCarotte(
        this IServiceCollection services,
        Action<CarotteOptions> configureOptions,
        Action<CarotteBuilder>? configure = null)
    {
        var options = new CarotteOptions();
        configureOptions(options);

        services.Configure(configureOptions);

        return services.AddCarotteCore(options, configure);
    }

    private static IServiceCollection AddCarotteCore(
        this IServiceCollection services,
        CarotteOptions? options,
        Action<CarotteBuilder>? configure)
    {
        var builder = new CarotteBuilder();

        if (options != null)
        {
            if (!string.IsNullOrWhiteSpace(options.ClientName))
            {
                builder.SetClientName(options.ClientName);
            }

            foreach (var (brokerName, brokerOptions) in options.Brokers)
            {
                builder.AddBroker(brokerName, brokerOptions);
            }

            foreach (var (consumerName, consumerOptions) in options.Consumers)
            {
                builder.ConsumerSettings[consumerName] = consumerOptions;
            }

            if (options.Serialization?.JsonSerializerOptions != null)
            {
                builder.SetJsonSerializerOptions(options.Serialization.JsonSerializerOptions);
            }
        }

        configure?.Invoke(builder);

        services.AddOptions();
        if (options != null)
        {
            services.TryAddSingleton(Options.Create(options));
        }
        else
        {
            var defaultOptions = new CarotteOptions
            {
                ClientName = builder.ClientName,
                Brokers = new Dictionary<string, RabbitMqOptions>(builder.Brokers, StringComparer.OrdinalIgnoreCase),
                Consumers = new Dictionary<string, ConsumerSettingsOptions>(builder.ConsumerSettings, StringComparer.OrdinalIgnoreCase),
                Serialization = builder.CustomJsonSerializerOptions != null
                    ? new CarotteSerializationOptions { JsonSerializerOptions = builder.CustomJsonSerializerOptions }
                    : null
            };
            services.TryAddSingleton(Options.Create(defaultOptions));
        }

        var (consumerScanResults, publisherScanResults) = builder.Assemblies.Scan(builder.Namespaces);

        var messageBrokerSettings = TopologyProvider.CreateSettings(
            builder.Brokers,
            consumerScanResults,
            publisherScanResults,
            builder.ClientName,
            builder.ConsumerSettings);

        var validationResult = CarotteBuilderValidator.Validate(messageBrokerSettings);

        if (!validationResult.IsSuccess)
        {
            throw new CarotteConfigurationException($"Carotte configuration validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
        }

        services.AddLogging();
        services.TryAddSingleton<IConnectionManager>(_ => new ConnectionManager(builder.Brokers));
        services.TryAddTransient<IRabbitMqClient, RabbitMqClient>();

        if (builder.CustomJsonSerializerOptions != null)
        {
            services.TryAddSingleton<ISerializer>(_ => new JsonSerializerImpl(builder.CustomJsonSerializerOptions));
        }
        else
        {
            services.TryAddSingleton<ISerializer, JsonSerializerImpl>();
        }

        AddOpenTelemetrySupport(services, builder);
        AddConsumers(services, messageBrokerSettings);
        AddPublishers(services, messageBrokerSettings);

        foreach (var action in builder.PostConfigureActions)
        {
            action(services);
        }

        services.TryAddSingleton(builder);

        return services;
    }

    private static void AddOpenTelemetrySupport(IServiceCollection services, CarotteBuilder builder)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(CarotteDiagnostics.ServiceName))
            .WithTracing(t =>
            {
                t.AddSource(CarotteDiagnostics.ServiceName);
                if (builder.OtlpEndpoint != null)
                {
                    t.AddOtlpExporter(opt => opt.Endpoint = builder.OtlpEndpoint);
                }
            })
            .WithMetrics(m =>
            {
                m.AddMeter(CarotteDiagnostics.ServiceName);
                if (builder.OtlpEndpoint != null)
                {
                    m.AddOtlpExporter(opt => opt.Endpoint = builder.OtlpEndpoint);
                }
            });
    }

    private static void AddConsumers(IServiceCollection services, MessageBrokerSettings messageBrokerSettings)
    {
        foreach (var consumer in messageBrokerSettings.Consumers)
        {
            services.AddScoped(consumer.ConsumerType);
            services.AddTransient<ConsumerMediator>();
            
            services.AddSingleton(typeof(IHostedService), sp =>
                ActivatorUtilities.CreateInstance(sp, typeof(RabbitMqConsumerHost<>).MakeGenericType(consumer.ConsumerType),
                    consumer.Broker,
                    consumer.Topology));
        }
    }

    private static void AddPublishers(IServiceCollection services, MessageBrokerSettings messageBrokerSettings)
    {
        foreach (var producer in messageBrokerSettings.Producers)
        {
            var interfaceTypeToRegister = typeof(IPublisher<>).MakeGenericType(producer.MessageType);
            services.TryAddSingleton(interfaceTypeToRegister, sp =>
            {
                var implementationType = typeof(RabbitMqPublisher<>).MakeGenericType(producer.MessageType);

                return ActivatorUtilities.CreateInstance(
                    sp,
                    implementationType,
                    producer.Broker,
                    producer.ExchangePublication,
                    producer.RoutingKey,
                    producer.ExchangeType,
                    producer.DeclareExchange,
                    producer.Durable,
                    producer.AutoDelete);
            });
        }
    }
}

public class CarotteBuilder
{
    internal Dictionary<string, RabbitMqOptions> Brokers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, ConsumerSettingsOptions> ConsumerSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal HashSet<string> Namespaces { get; } = [];
    internal Uri? OtlpEndpoint { get; private set; }
    public string? ClientName { get; private set; }
    public JsonSerializerOptions? CustomJsonSerializerOptions { get; private set; }

    // Extension points for test mode without modifying AddCarotte logic
    public List<Action<IServiceCollection>> PostConfigureActions { get; } = [];

    public CarotteBuilder SetClientName(string name)
    {
        ClientName = name;
        return this;
    }

    public CarotteBuilder SetJsonSerializerOptions(JsonSerializerOptions options)
    {
        CustomJsonSerializerOptions = options;
        return this;
    }

    public CarotteBuilder AddOtlpExporter(string endpoint)
    {
        OtlpEndpoint = new Uri(endpoint);
        return this;
    }

    public CarotteBuilder AddBroker(string name, Action<RabbitMqOptions> configure)
    {
        var options = new RabbitMqOptions();
        configure(options);
        Brokers[name] = options;
        return this;
    }

    public CarotteBuilder AddBroker(string name, RabbitMqOptions options)
    {
        Brokers[name] = options;
        return this;
    }

    public CarotteBuilder ConfigureConsumer(string consumerName, Action<ConsumerSettingsOptions> configure)
    {
        if (!ConsumerSettings.TryGetValue(consumerName, out var settings))
        {
            settings = new ConsumerSettingsOptions();
            ConsumerSettings[consumerName] = settings;
        }
        configure(settings);
        return this;
    }

    public CarotteBuilder AddAssemblies(params Assembly[] assemblies)
    {
        Assemblies.UnionWith(assemblies);
        return this;
    }

    public CarotteBuilder AddNamespaces(params string[] namespaces)
    {
        foreach (var ns in namespaces)
        {
            Namespaces.Add(ns);
        }
        return this;
    }
}
