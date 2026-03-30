using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<IConnectionManager>(sp => new ConnectionManager(builder.Brokers));
        services.AddSingleton<ITopologyManager, TopologyManager>();
        services.AddSingleton<ISerializer, JsonSerializerImpl>();

        // Configuration OpenTelemetry
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

        // Enregistrement automatique des consommateurs
        var consumerTypes = builder.Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)));

        foreach (var consumerType in consumerTypes)
        {
            // On enregistre le consommateur lui-même en tant que Singleton selon les instructions
            services.AddSingleton(consumerType);

            // Récupération de la configuration via attribut ou configuration explicite
            var queueAttrs = consumerType.GetCustomAttributes<QueueAttribute>().ToList();
            
            if (builder.ConsumerConfigs.TryGetValue(consumerType, out var config))
            {
                // Priorité à la configuration explicite si présente
                queueAttrs = [new QueueAttribute(config.Queue, config.Broker)];
            }

            if (!queueAttrs.Any()) 
                continue;
            var broker = queueAttrs.First().Broker;
            services.AddSingleton(typeof(IHostedService), sp => 
                ActivatorUtilities.CreateInstance(sp, typeof(RabbitMQConsumerHost<>).MakeGenericType(consumerType), broker, queueAttrs));
        }

        // Enregistrement des producteurs
        foreach (var prodConfig in builder.ProducerConfigs)
        {
            var interfaceType = typeof(IProducer<>).MakeGenericType(prodConfig.MessageType);
            var implementationType = typeof(RabbitMQProducer<>).MakeGenericType(prodConfig.MessageType);

            services.AddSingleton(interfaceType, sp =>
                ActivatorUtilities.CreateInstance(sp, implementationType, prodConfig.Broker, prodConfig.Exchange));
        }

        return services;
    }
}

public class CarotteBuilder
{
    public IServiceCollection Services { get; }
    public Dictionary<string, RabbitMQOptions> Brokers { get; } = new();
    public List<Assembly> Assemblies { get; } = [];
    public Dictionary<Type, (string Broker, string Queue)> ConsumerConfigs { get; } = new();
    public List<(Type MessageType, string Broker, string Exchange)> ProducerConfigs { get; } = [];
    public Uri? OtlpEndpoint { get; private set; }

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

    public CarotteBuilder AddProducer<TMessage>(string broker, string exchange)
    {
        ProducerConfigs.Add((typeof(TMessage), broker, exchange));
        return this;
    }

    public CarotteBuilder AddBroker(string name, Action<RabbitMQOptions> configure)
    {
        var options = new RabbitMQOptions();
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
