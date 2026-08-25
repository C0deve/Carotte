using Carotte.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Carotte;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCarotte(Action<CarotteBuilder> configure) =>
            services.AddCarotteCore(null, configure);

        public IServiceCollection AddCarotte(IConfiguration configuration,
            Action<CarotteBuilder>? configure = null)
        {
            var options = new CarotteOptions();
            configuration.Bind(options);

            services.Configure<CarotteOptions>(configuration);

            return services.AddCarotteCore(options, configure);
        }

        public IServiceCollection AddCarotte(Action<CarotteOptions> configureOptions,
            Action<CarotteBuilder>? configure = null)
        {
            var options = new CarotteOptions();
            configureOptions(options);

            services.Configure(configureOptions);

            return services.AddCarotteCore(options, configure);
        }

        private IServiceCollection AddCarotteCore(CarotteOptions? options,
            Action<CarotteBuilder>? configure)
        {
            var builder = CreateBuilder(options, configure);
            var messageBrokerSettings = CreateAndValidateSettings(builder);

            services
                .AddCarotteOptions(options, builder)
                .AddCoreServices(builder)
                .AddOpenTelemetrySupport(builder)
                .AddConsumers(messageBrokerSettings)
                .AddPublishers(messageBrokerSettings)
                .TryAddSingleton(builder);

            return services;
        }

        private IServiceCollection AddCarotteOptions(CarotteOptions? options, CarotteBuilder builder)
        {
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

            return services;
        }

        private IServiceCollection AddCoreServices(CarotteBuilder builder)
        {
            services.AddLogging();
            services.TryAddSingleton<IConnectionManager>(_ => new ConnectionManager(builder.Brokers));
            services.TryAddTransient<IRabbitMqClient, RabbitMqClient>();

            if (builder.CustomJsonSerializerOptions == null)
            {
                services.TryAddSingleton<ISerializer, JsonSerializerImpl>();
            }
            else
            {
                services.TryAddSingleton<ISerializer>(_ => new JsonSerializerImpl(builder.CustomJsonSerializerOptions));
            }

            return services;
        }

        private IServiceCollection AddOpenTelemetrySupport(CarotteBuilder builder)
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

            return services;
        }

        private IServiceCollection AddConsumers(MessageBrokerSettings messageBrokerSettings)
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

            return services;
        }

        private IServiceCollection AddPublishers(MessageBrokerSettings messageBrokerSettings)
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

            return services;
        }
    }

    private static CarotteBuilder CreateBuilder(CarotteOptions? options, Action<CarotteBuilder>? configure)
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
        return builder;
    }

    private static MessageBrokerSettings CreateAndValidateSettings(this CarotteBuilder builder)
    {
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
            throw new CarotteConfigurationException(
                $"Carotte configuration validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
        }

        return messageBrokerSettings;
    }
}