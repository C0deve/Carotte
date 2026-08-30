using System.Collections.ObjectModel;
using System.Reflection;

namespace Carotte;

/// <summary>
/// Scans specified assemblies for consumers implementing <see cref="IConsumer{TMessage}"/> and messages decorated with <see cref="PublishedAttribute"/>.
/// </summary>
internal static class CarotteScanner
{
    /// <summary>
    /// Scans the provided assemblies and extracts all consumer and publisher declarations.
    /// Filters types by namespace (if specified) and excludes abstract classes.
    /// </summary>
    public static (ReadOnlyCollection<ConsumerScanResult>, ReadOnlyCollection<PublisherScanResult>) Scan(
        this HashSet<Assembly> assemblies,
        IReadOnlyCollection<string>? namespaces = null)
    {
        namespaces ??= [];
        var types = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null);
                }
            })
            .Where(t => t != null)
            .Cast<Type>()
            .Where(t => namespaces.Count == 0 ||
                        (t.Namespace != null &&
                         namespaces.Any(ns => t.Namespace == ns || t.Namespace.StartsWith(ns + "."))))
            .Where(t => t is not { IsAbstract: true })
            .ToList()
            .AsReadOnly();

        var consumerScanResults = ScanConsumers(types);
        var producerScanResults = ScanProducers(types);

        return (consumerScanResults, producerScanResults);
    }

    /// <summary>
    /// Scans types for classes implementing <see cref="IConsumer{TMessage}"/> and extracts their <see cref="QueueAttribute"/> and <see cref="BindingAttribute"/> metadata.
    /// </summary>
    private static ReadOnlyCollection<ConsumerScanResult> ScanConsumers(IReadOnlyList<Type> types)
    {
        var consumerTypeAndInterfaces =
            from type in types
            let interfaces =
                from i in type.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)
                select i
            where interfaces.Any()
            select (ConsumerType: type, IConsumerInterfaces: interfaces);

        var consumerScanResult =
            from x in consumerTypeAndInterfaces
            let messageTypes =
                from i in x.IConsumerInterfaces
                select i.GetGenericArguments()[0]
            let queueAttr = x.ConsumerType.GetCustomAttribute<QueueAttribute>()
            let bindingAttrs = x.ConsumerType.GetCustomAttributes<BindingAttribute>()
            select new ConsumerScanResult(x.ConsumerType,
                messageTypes.ToList().AsReadOnly(),
                queueAttr,
                bindingAttrs.ToList().AsReadOnly());

        return consumerScanResult.ToList().AsReadOnly();
    }

    /// <summary>
    /// Scans types for message contracts decorated with <see cref="PublishedAttribute"/>.
    /// </summary>
    private static ReadOnlyCollection<PublisherScanResult> ScanProducers(IReadOnlyList<Type> types)
    {
        // Scan for messages marked with [Published]
        var messagesWithAttr =
            from type in types
            let publishedAttr = type.GetCustomAttribute<PublishedAttribute>()
            where publishedAttr != null
            select new PublisherScanResult(type, publishedAttr!);

        return messagesWithAttr.ToList().AsReadOnly();
    }
}

/// <summary>
/// Holds scanned metadata for a consumer class.
/// </summary>
internal readonly record struct ConsumerScanResult(
    Type ConsumerType,
    ReadOnlyCollection<Type> MessageTypes,
    QueueAttribute? QueueAttr,
    ReadOnlyCollection<BindingAttribute> BindingAttrs);

/// <summary>
/// Holds scanned metadata for a message type configured for publishing.
/// </summary>
internal readonly record struct PublisherScanResult(Type MessageType, PublishedAttribute PublishedAttribute);