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
                        .Select(b => new BindingInfo(
                            b.Exchange,
                            b.RoutingKey,
                            b.ExchangeType,
                            b.DeclareExchange,
                            b.Durable,
                            b.AutoDelete))
                        .Union([new BindingInfo(
                            scan.QueueAttr!.Exchange ?? "",
                            scan.QueueAttr.RoutingKey,
                            scan.QueueAttr.ExchangeType,
                            scan.QueueAttr.DeclareExchange,
                            scan.QueueAttr.ExchangeDurable,
                            scan.QueueAttr.ExchangeAutoDelete)])
                        .ToList()
                        .AsReadOnly(),
                    PrefetchCount: prefetchCount,
                    ErrorStrategy: errorStrategy,
                    QueueDurable: scan.QueueAttr.Durable,
                    QueueExclusive: scan.QueueAttr.Exclusive,
                    QueueAutoDelete: scan.QueueAttr.AutoDelete);
        }
    }

    private static ProducerInfo ToProducerInfo(this PublisherScanResult scan)
    {
        var usesConvention = string.IsNullOrWhiteSpace(scan.PublisherAttribute.Exchange);

        return new ProducerInfo(
            scan.MessageType,
            scan.PublisherAttribute.Broker ?? string.Empty,
            usesConvention
                ? scan.MessageType.Name.ToDefaultExchangeName()
                : scan.PublisherAttribute.Exchange!,
            scan.PublisherAttribute.RoutingKey ?? (usesConvention ? string.Empty : scan.MessageType.Name),
            usesConvention ? ExchangeType.Fanout : scan.PublisherAttribute.ExchangeType,
            usesConvention || scan.PublisherAttribute.DeclareExchange,
            scan.PublisherAttribute.Durable,
            scan.PublisherAttribute.AutoDelete);
    }

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
