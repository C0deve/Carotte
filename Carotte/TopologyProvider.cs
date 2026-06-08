using System.Collections.ObjectModel;

namespace Carotte;

internal static class TopologyProvider
{
    extension(ConsumerScanResult scan)
    {
        private ConsumerInfo ToConsumerInfo(string? clientName) => new(
            scan.ConsumerType,
            [..scan.MessageTypes],
            scan.QueueAttr?.Broker ?? string.Empty,
            scan.ToConsumerTopology(clientName)
        );

        private IConsumerTopology ToConsumerTopology(string? clientName) => scan.QueueAttr == null
            ? new ConsumerConventionTopology(
                Broker: scan.QueueAttr?.Broker ?? string.Empty,
                Queue: scan.ConsumerType.Name.ToConsumerQueueName(clientName),
                ConsumerExchangeName: scan.ConsumerType.Name.ToConsumerExchangeName(clientName),
                MessageExchangeNames: scan.MessageTypes
                    .Select(m => m.Name.ToMessageExchangeName())
                    .ToList()
                    .AsReadOnly())
            : new ConsumerAttributeTopology(
                Broker: scan.QueueAttr?.Broker ?? string.Empty,
                Queue: scan.QueueAttr?.Name ?? scan.ConsumerType.Name.ToDefaultQueueName(),
                Bindings: scan.BindingAttrs
                    .Select(b => new BindingInfo(b.Exchange, b.RoutingKey))
                    .Union([new BindingInfo(scan.QueueAttr!.Exchange ?? "", scan.QueueAttr.RoutingKey)])
                    .ToList()
                    .AsReadOnly());
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
                .Select(sc => sc.ToConsumerInfo(clientName))
                .Select(info => string.IsNullOrEmpty(info.Broker)
                    ? info with { Broker = firstBrokerName }
                    : info)
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