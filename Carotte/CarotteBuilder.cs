using System.Reflection;
using System.Text.Json;

namespace Carotte;

public class CarotteBuilder
{
    internal Dictionary<string, RabbitMqOptions> Brokers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, ConsumerSettingsOptions> ConsumerSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal HashSet<string> Namespaces { get; } = [];
    internal Uri? OtlpEndpoint { get; private set; }
    public string? ClientName { get; private set; }
    public JsonSerializerOptions? CustomJsonSerializerOptions { get; private set; }
    
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