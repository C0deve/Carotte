using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte;

public class CarotteBuilder
{
    internal Dictionary<string, RabbitMqOptions> Brokers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, ConsumerSettingsOptions> ConsumerSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal HashSet<string> Namespaces { get; } = [];
    internal List<Action<IServiceCollection>> ServiceConfigurators { get; } = [];
    internal Uri? OtlpEndpoint { get; private set; }
    public string? ServiceName { get; private set; }
    public JsonSerializerOptions? CustomJsonSerializerOptions { get; private set; }

    public CarotteBuilder WithServiceName(string serviceName)
    {
        ServiceName = serviceName;
        return this;
    }

    public CarotteBuilder WithServiceNameFromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        return WithServiceNameFrom(assembly);
    }

    public CarotteBuilder WithServiceNameFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var name = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Could not determine service name from assembly.");
        }
        return WithServiceName(name);
    }

    public CarotteBuilder WithServiceNameFrom<T>() => WithServiceNameFrom(typeof(T).Assembly);

    public CarotteBuilder WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        CustomJsonSerializerOptions = options;
        return this;
    }

    public CarotteBuilder ConfigureJsonSerializer(Action<JsonSerializerOptions> configure)
    {
        var options = CustomJsonSerializerOptions ?? new JsonSerializerOptions();
        configure(options);
        CustomJsonSerializerOptions = options;
        return this;
    }

    public CarotteBuilder WithOtlpExporter(string endpoint) => WithOtlpExporter(new Uri(endpoint));

    public CarotteBuilder WithOtlpExporter(Uri endpoint)
    {
        OtlpEndpoint = endpoint;
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

    public CarotteBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        Assemblies.UnionWith(assemblies);
        return this;
    }

    public CarotteBuilder ScanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Assemblies.Add(assembly);
        return this;
    }

    public CarotteBuilder ScanAssemblyContaining<T>() => ScanAssembly(typeof(T).Assembly);

    public CarotteBuilder ScanNamespaces(params string[] namespaces)
    {
        foreach (var ns in namespaces)
        {
            Namespaces.Add(ns);
        }
        return this;
    }

    public CarotteBuilder ScanNamespace(string @namespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        Namespaces.Add(@namespace);
        return this;
    }

    public CarotteBuilder ScanNamespaceOf<T>()
    {
        var ns = typeof(T).Namespace;
        if (string.IsNullOrWhiteSpace(ns))
        {
            throw new InvalidOperationException($"Type {typeof(T).FullName} does not have a namespace.");
        }
        return ScanNamespace(ns);
    }

    public CarotteBuilder AddCustomServiceConfigurator(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ServiceConfigurators.Add(configure);
        return this;
    }
}