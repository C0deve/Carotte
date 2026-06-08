using System.Collections.ObjectModel;
using System.Reflection;

namespace Carotte;

internal static class CarotteScanner
{
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

    private static ReadOnlyCollection<PublisherScanResult> ScanProducers(IReadOnlyList<Type> types)
    {
        // Scan for messages marked with [Publisher]
        var messagesWithAttr =
            from type in types
            let publisherAttr = type.GetCustomAttribute<PublisherAttribute>()
            where publisherAttr != null
            select new PublisherScanResult(type, publisherAttr!);

        return messagesWithAttr.ToList().AsReadOnly();
    }
}

internal readonly record struct ConsumerScanResult(Type ConsumerType, ReadOnlyCollection<Type> MessageTypes, QueueAttribute? QueueAttr, ReadOnlyCollection<BindingAttribute> BindingAttrs);

internal readonly record struct PublisherScanResult(Type MessageType, PublisherAttribute PublisherAttribute);