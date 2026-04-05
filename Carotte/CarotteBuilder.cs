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
    public static IServiceCollection AddCarotte(this IServiceCollection services, Action<CarotteBuilder> configure)
    {
        var builder = new CarotteBuilder(services);
        configure(builder);

        services.TryAddSingleton<IConnectionManager>(sp => new ConnectionManager(builder.Brokers));
        services.TryAddSingleton<ITopologyManager, TopologyManager>();
        services.TryAddSingleton<ISerializer, JsonSerializerImpl>();

        // OpenTelemetry configuration
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

        // Automatic consumer registration
        var consumerTypes = builder.Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)));

        foreach (var consumerType in consumerTypes)
        {
            // Register the consumer itself as a Singleton per instructions
            services.AddSingleton(consumerType);

            // Register the mediator for the consumer
            services.AddTransient<ConsumerMediator>();

            // Get configuration via attribute or explicit configuration
            var queueAttrs = consumerType.GetCustomAttributes<QueueAttribute>().ToList();

            if (builder.ConsumerConfigs.TryGetValue(consumerType, out var config))
            {
                // Priority to explicit configuration if present
                queueAttrs = [new QueueAttribute(config.Queue, config.Broker)];
            }

            if (queueAttrs.Count == 0)
                continue;

            var broker = queueAttrs.First().Broker;
            services.AddSingleton(typeof(IHostedService), sp =>
                ActivatorUtilities.CreateInstance(sp, typeof(RabbitMqConsumerHost<>).MakeGenericType(consumerType), broker, queueAttrs));
        }

        // Producer registration
        foreach (var prodConfig in builder.ProducerConfigs)
        {
            var interfaceType = typeof(IProducer<>).MakeGenericType(prodConfig.MessageType);

            var implementationType = typeof(RabbitMqProducer<>).MakeGenericType(prodConfig.MessageType);

            services.TryAddSingleton(interfaceType, sp =>
                ActivatorUtilities.CreateInstance(sp, implementationType, prodConfig.Broker, prodConfig.Exchange));
        }

        foreach (var action in builder.PostConfigureActions)
        {
            action(services);
        }

        return services;
    }
}

public class CarotteBuilder
{
    public IServiceCollection Services { get; }
    public Dictionary<string, RabbitMqOptions> Brokers { get; } = [];
    public List<Assembly> Assemblies { get; } = [];
    public Dictionary<Type, (string Broker, string Queue)> ConsumerConfigs { get; } = [];
    public List<(Type MessageType, string Broker, string Exchange)> ProducerConfigs { get; } = [];
    public Uri? OtlpEndpoint { get; private set; }

    // Extension points for test mode without modifying AddCarotte logic
    public List<Action<IServiceCollection>> PostConfigureActions { get; } = [];

    public CarotteBuilder(IServiceCollection services)
    {
        Services = services;
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