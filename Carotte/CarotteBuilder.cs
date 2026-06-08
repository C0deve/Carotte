using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Carotte.Exceptions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Carotte;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCarotte(this IServiceCollection services, Action<CarotteBuilder> configure)
        {
            var builder = new CarotteBuilder();
            configure(builder);

            var (consumerScanResults, publisherScanResults) = builder.Assemblies.Scan(builder.Namespaces);

            var messageBrokerSettings = TopologyProvider.CreateSettings(
                builder.Brokers,
                consumerScanResults,
                publisherScanResults);

            var validationResult = CarotteBuilderValidator.Validate(messageBrokerSettings);

            if (!validationResult.IsSuccess)
            {
                throw new CarotteConfigurationException($"Carotte configuration validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
            }

            services.AddLogging();
            services.TryAddSingleton<IConnectionManager>(_ => new ConnectionManager(builder.Brokers));
            services.TryAddTransient<IRabbitMqClient, RabbitMqClient>();
            services.TryAddSingleton<ITopologyManager, TopologyManager>();
            services.TryAddSingleton<ISerializer, JsonSerializerImpl>();

            AddOpenTelemetrySupport(services, builder);
            AddConsumers(services, builder, messageBrokerSettings);
            AddPublishers(services, builder, messageBrokerSettings);

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

        private static void AddConsumers(IServiceCollection services, CarotteBuilder builder, MessageBrokerSettings messageBrokerSettings)
        {
            foreach (var consumer in messageBrokerSettings.Consumers)
            {
                services.AddSingleton(consumer.ConsumerType);
                services.AddTransient<ConsumerMediator>();
                
                services.AddSingleton(typeof(IHostedService), sp =>
                    ActivatorUtilities.CreateInstance(sp, typeof(RabbitMqConsumerHost<>).MakeGenericType(consumer.ConsumerType),
                        consumer.Broker,
                        consumer.Topology));
            }
        }

        private static void AddPublishers(IServiceCollection services, CarotteBuilder builder, MessageBrokerSettings messageBrokerSettings)
        {
            foreach (var producer in messageBrokerSettings.Producers)
            {
                var interfaceTypeToRegister = typeof(IPublisher<>).MakeGenericType(producer.MessageType);
                services.TryAddSingleton(interfaceTypeToRegister, sp =>
                {
                    var implementationType = typeof(RabbitMqPublisher<>).MakeGenericType(producer.MessageType);

                    var exchange = producer.ExchangePublication;
                    var client = sp.GetRequiredService<IRabbitMqClient>();
                    var serializer = sp.GetRequiredService<ISerializer>();
                    return Activator.CreateInstance(implementationType, client, serializer, producer.Broker, exchange)!;
                });
            }
        }
    }

public class CarotteBuilder
{
    internal Dictionary<string, RabbitMqOptions> Brokers { get; } = [];
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal HashSet<string> Namespaces { get; } = [];
    internal Uri? OtlpEndpoint { get; private set; }

    // Extension points for test mode without modifying AddCarotte logic
    public List<Action<IServiceCollection>> PostConfigureActions { get; } = [];

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