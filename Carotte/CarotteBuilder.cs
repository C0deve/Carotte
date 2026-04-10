using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Carotte;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCarotte(Action<CarotteBuilder> configure)
        {
            var builder = new CarotteBuilder();
            configure(builder);

            services.TryAddSingleton<IConnectionManager>(_ => new ConnectionManager(builder.Brokers));
            services.TryAddSingleton<IRabbitMqClient, RabbitMqClient>();
            services.TryAddSingleton<ITopologyManager, TopologyManager>();
            services.TryAddSingleton<ISerializer, JsonSerializerImpl>();

            services.AddOpenTelemetrySupport(builder);
            services.AddConsumers(builder);
            services.AddProducers(builder);

            foreach (var action in builder.PostConfigureActions)
            {
                action(services);
            }

            services.TryAddSingleton(builder);

            return services;
        }

        private void AddOpenTelemetrySupport(CarotteBuilder builder)
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

        private void AddConsumers(CarotteBuilder builder)
        {
            var consumerTypes = builder.Assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)));

            foreach (var consumerType in consumerTypes)
            {
                services.AddSingleton(consumerType);
                services.AddTransient<ConsumerMediator>();

                var queueAttrs = consumerType.GetCustomAttributes<QueueAttribute>().ToList();

                if (builder.ConsumerConfigs.TryGetValue(consumerType, out var config))
                {
                    queueAttrs = [new QueueAttribute(config.Queue, config.Broker)];
                }

                if (queueAttrs.Count == 0)
                    continue;

                var broker = queueAttrs.First().Broker;
                services.AddSingleton(typeof(IHostedService), sp =>
                    ActivatorUtilities.CreateInstance(sp, typeof(RabbitMqConsumerHost<>).MakeGenericType(consumerType), broker, queueAttrs));
            }
        }

        private void AddProducers(CarotteBuilder builder)
        {
            foreach (var prodConfig in builder.ProducerConfigs)
            {
                var interfaceType = typeof(IProducer<>).MakeGenericType(prodConfig.MessageType);
                var implementationType = typeof(RabbitMqProducer<>).MakeGenericType(prodConfig.MessageType);

                services.TryAddSingleton(interfaceType, sp =>
                    ActivatorUtilities.CreateInstance(sp, implementationType, prodConfig.Broker, prodConfig.Exchange));
            }
        }
    }
}

public class CarotteBuilder
{
    public Dictionary<string, RabbitMqOptions> Brokers { get; } = [];
    public List<Assembly> Assemblies { get; } = [];
    public Dictionary<Type, (string Broker, string Queue)> ConsumerConfigs { get; } = [];
    public List<(Type MessageType, string Broker, string Exchange)> ProducerConfigs { get; } = [];
    public Uri? OtlpEndpoint { get; private set; }

    // Extension points for test mode without modifying AddCarotte logic
    public List<Action<IServiceCollection>> PostConfigureActions { get; } = [];

    public CarotteBuilder()
    {
        Assemblies.Add(Assembly.GetCallingAssembly());
    }

    public CarotteBuilder AddOtlpExporter(string endpoint)
    {
        OtlpEndpoint = new Uri(endpoint);
        return this;
    }

    public CarotteBuilder AddProducer<TMessage>(string broker, string exchange) where TMessage : class
    {
        ProducerConfigs.Add((typeof(TMessage), broker, exchange));
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
        foreach (var assembly in assemblies)
        {
            if (!Assemblies.Contains(assembly))
            {
                Assemblies.Add(assembly);
            }
        }

        return this;
    }
}