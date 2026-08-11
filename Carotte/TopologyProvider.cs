using System.Collections.ObjectModel;

namespace Carotte;

internal static class TopologyProvider
{
    extension(ConsumerScanResult scan)
    {
        private ConsumerInfo ToConsumerInfo(string? clientName, ushort defaultPrefetchCount) => new(
            scan.ConsumerType,
            [..scan.MessageTypes],
            scan.QueueAttr?.Broker ?? string.Empty,
            scan.ToConsumerTopology(clientName, defaultPrefetchCount)
        );

        private IConsumerTopology ToConsumerTopology(string? clientName, ushort defaultPrefetchCount)
        {
            var prefetchCount = scan.QueueAttr?.PrefetchCount ?? defaultPrefetchCount;
            var queueName = scan.QueueAttr?.Name ?? scan.ConsumerType.Name.ToConsumerQueueName(clientName);
            var errorStrategy = scan.QueueAttr == null
                ? ConsumerErrorStrategy.ByConvention(queueName)
                : new ConsumerErrorStrategy(
                    scan.QueueAttr.MaxRetryAttempts,
                    scan.QueueAttr.FailureAction,
                    scan.QueueAttr.DeadLetterExchange,
                    scan.QueueAttr.DeadLetterRoutingKey,
                    scan.QueueAttr.DeadLetterQueue).WithConventionDefaults(queueName);

            return scan.QueueAttr == null
                ? new ConsumerConventionTopology(
                    Broker: scan.QueueAttr?.Broker ?? string.Empty,
                    Queue: queueName,
                    ConsumerExchangeName: scan.ConsumerType.Name.ToConsumerExchangeName(clientName),
                    MessageExchangeNames: scan.MessageTypes
                        .Select(m => m.Name.ToMessageExchangeName())
                        .ToList()
                        .AsReadOnly(),
                    PrefetchCount: prefetchCount,
                    ErrorStrategy: errorStrategy)
                : new ConsumerAttributeTopology(
                    Broker: scan.QueueAttr?.Broker ?? string.Empty,
                    Queue: queueName,
                    Bindings: scan.BindingAttrs
                        .Select(b => new BindingInfo(b.Exchange, b.RoutingKey))
                        .Union([new BindingInfo(scan.QueueAttr!.Exchange ?? "", scan.QueueAttr.RoutingKey)])
                        .ToList()
                        .AsReadOnly(),
                    PrefetchCount: prefetchCount,
                    ErrorStrategy: errorStrategy);
        }
    }

    private static ProducerInfo ToProducerInfo(this PublisherScanResult scan) =>
        new(scan.MessageType,
            scan.PublisherAttribute.Broker ?? string.Empty,
            scan.PublisherAttribute.Exchange ?? scan.MessageType.Name.ToDefaultExchangeName());

    public static MessageBrokerSettings CreateSettings(
        Dictionary<string, RabbitMqOptions> brokers,
        ReadOnlyCollection<ConsumerScanResult> consumerScanResults,
        ReadOnlyCollection<PublisherScanResult> publisherScanResults,
        string? clientName = null)
    {
        var firstBrokerName = brokers.Keys.FirstOrDefault() ?? string.Empty;

        var brokerInfos = brokers.ToDictionary(
            kvp => kvp.Key,
            kvp => new BrokerInfos(kvp.Value.Host, kvp.Value.Port, kvp.Value.UserName, kvp.Value.Password)
        );

        var consumers =
            consumerScanResults
                .Select(sc =>
                {
                    var brokerName = sc.QueueAttr?.Broker ?? firstBrokerName;
                    var prefetchCount = brokers.GetValueOrDefault(brokerName)?.DefaultPrefetchCount ?? 1;
                    return sc.ToConsumerInfo(clientName, prefetchCount) with { Broker = brokerName };
                })
                .ToList()
                .AsReadOnly();

        var producers = publisherScanResults
            .Select(sc => sc.ToProducerInfo())
            .Select(info => string.IsNullOrEmpty(info.Broker)
                ? info with { Broker = firstBrokerName }
                : info)
            .ToList()
            .AsReadOnly();

        return new MessageBrokerSettings(
            brokerInfos.AsReadOnly(),
            consumers,
            producers
        );
    }
}
