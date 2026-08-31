using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte;

/// <summary>
/// Fluent builder for configuring Carotte messaging infrastructure, brokers, consumers, and reflection scan scope.
/// </summary>
public class CarotteBuilder
{
    internal Dictionary<string, RabbitMqOptions> Brokers { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, ConsumerSettingsOptions> ConsumerSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal HashSet<string> Namespaces { get; } = [];
    internal List<Action<IServiceCollection>> ServiceConfigurators { get; } = [];
    internal Uri? OtlpEndpoint { get; private set; }

    /// <summary>
    /// Gets the configured logical service name used for convention-based topology naming (e.g. queues, exchanges) and telemetry.
    /// </summary>
    public string? ServiceName { get; private set; }

    /// <summary>
    /// Gets the custom <see cref="JsonSerializerOptions"/> configured for message serialization and deserialization.
    /// </summary>
    public JsonSerializerOptions? CustomJsonSerializerOptions { get; private set; }

    /// <summary>
    /// Configures an explicit service name for convention-based topology naming and observability.
    /// </summary>
    /// <param name="serviceName">The unique service identifier (e.g., "order-service").</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder WithServiceName(string serviceName)
    {
        ServiceName = serviceName;
        return this;
    }

    /// <summary>
    /// Configures the service name by inferring it from the entry or calling assembly name.
    /// </summary>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the assembly name cannot be determined.</exception>
    public CarotteBuilder WithServiceNameFromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        return WithServiceNameFrom(assembly);
    }

    /// <summary>
    /// Configures the service name by deriving it from the specified assembly name.
    /// </summary>
    /// <param name="assembly">The target assembly to extract the service name from.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly name cannot be determined.</exception>
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

    /// <summary>
    /// Configures the service name by deriving it from the assembly containing the type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A marker type located in the target service assembly.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder WithServiceNameFrom<T>() => WithServiceNameFrom(typeof(T).Assembly);

    /// <summary>
    /// Configures custom <see cref="JsonSerializerOptions"/> to be used across the messaging pipeline.
    /// </summary>
    /// <param name="options">The JSON serialization options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        CustomJsonSerializerOptions = options;
        return this;
    }

    /// <summary>
    /// Configures the JSON serialization options via an action delegate.
    /// </summary>
    /// <param name="configure">The configuration delegate applied to <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder ConfigureJsonSerializer(Action<JsonSerializerOptions> configure)
    {
        var options = CustomJsonSerializerOptions ?? new JsonSerializerOptions();
        configure(options);
        CustomJsonSerializerOptions = options;
        return this;
    }

    /// <summary>
    /// Configures an OpenTelemetry Protocol (OTLP) exporter endpoint for telemetry and traces.
    /// </summary>
    /// <param name="endpoint">The OTLP collector URI endpoint (e.g., "http://localhost:4317").</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder WithOtlpExporter(string endpoint) => WithOtlpExporter(new Uri(endpoint));

    /// <summary>
    /// Configures an OpenTelemetry Protocol (OTLP) exporter endpoint for telemetry and traces.
    /// </summary>
    /// <param name="endpoint">The OTLP collector URI endpoint.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder WithOtlpExporter(Uri endpoint)
    {
        OtlpEndpoint = endpoint;
        return this;
    }

    /// <summary>
    /// Adds a named RabbitMQ broker configuration.
    /// </summary>
    /// <param name="name">The broker registration name.</param>
    /// <param name="configure">A delegate to configure the broker connection settings.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder AddBroker(string name, Action<RabbitMqOptions> configure)
    {
        var options = new RabbitMqOptions();
        configure(options);
        Brokers[name] = options;
        return this;
    }

    /// <summary>
    /// Adds a named RabbitMQ broker configuration using existing options.
    /// </summary>
    /// <param name="name">The broker registration name.</param>
    /// <param name="options">The pre-configured RabbitMQ options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder AddBroker(string name, RabbitMqOptions options)
    {
        Brokers[name] = options;
        return this;
    }

    /// <summary>
    /// Configures override settings for a specific consumer (e.g., retries, failure actions, routing keys).
    /// </summary>
    /// <param name="consumerName">The name or full type name of the consumer.</param>
    /// <param name="configure">A delegate to configure consumer settings.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
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

    /// <summary>
    /// Adds multiple assemblies to the reflection discovery scope for consumers and published messages.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        Assemblies.UnionWith(assemblies);
        return this;
    }

    /// <summary>
    /// Adds a single assembly to the reflection discovery scope.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <c>null</c>.</exception>
    public CarotteBuilder ScanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Assemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Adds the assembly containing the marker type <typeparamref name="T"/> to the discovery scope.
    /// </summary>
    /// <typeparam name="T">A type residing in the assembly to be scanned.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder ScanAssemblyContaining<T>() => ScanAssembly(typeof(T).Assembly);

    /// <summary>
    /// Restricts or filters type discovery to the specified namespaces.
    /// </summary>
    /// <param name="namespaces">The namespace prefixes to scan.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public CarotteBuilder ScanNamespaces(params string[] namespaces)
    {
        foreach (var ns in namespaces)
        {
            Namespaces.Add(ns);
        }
        return this;
    }

    /// <summary>
    /// Adds a namespace prefix to the reflection scan filter.
    /// </summary>
    /// <param name="namespace">The namespace prefix to include.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="namespace"/> is <c>null</c> or whitespace.</exception>
    public CarotteBuilder ScanNamespace(string @namespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        Namespaces.Add(@namespace);
        return this;
    }

    /// <summary>
    /// Adds the namespace of the marker type <typeparamref name="T"/> to the reflection scan filter.
    /// </summary>
    /// <typeparam name="T">A type whose namespace will be included in the discovery filter.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type does not have a namespace.</exception>
    public CarotteBuilder ScanNamespaceOf<T>()
    {
        var ns = typeof(T).Namespace;
        if (string.IsNullOrWhiteSpace(ns))
        {
            throw new InvalidOperationException($"Type {typeof(T).FullName} does not have a namespace.");
        }
        return ScanNamespace(ns);
    }

    /// <summary>
    /// Registers a custom service configurator delegate to extend service registration within the DI container.
    /// </summary>
    /// <param name="configure">The configuration delegate invoked with <see cref="IServiceCollection"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <c>null</c>.</exception>
    public CarotteBuilder AddCustomServiceConfigurator(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ServiceConfigurators.Add(configure);
        return this;
    }
}