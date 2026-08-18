using System.Collections.ObjectModel;

namespace Carotte;

internal static class TopologyProvider
{
    private static ConsumerSettingsOptions? FindConsumerSettings(
        Dictionary<string, ConsumerSettingsOptions>? settings,
        Type consumerType,
        string? queueName)
    {
        if (settings == null) return null;
        if (settings.TryGetValue(consumerType.Name, out var byName)) return byName;
        if (consumerType.FullName != null && settings.TryGetValue(consumerType.FullName, out var byFullName)) return byFullName;
        if (queueName != null && settings.TryGetValue(queueName, out var byQueue)) return byQueue;
        return null;
    }

    private static ConsumerInfo ToConsumerInfo(
        this ConsumerScanResult scan,
        string? clientName,
        ushort defaultPrefetchCount,
        ConsumerSettingsOptions? overrideSettings)
    {
        var broker = overrideSettings?.Broker ?? scan.QueueAttr?.Broker ?? string.Empty;
        return new ConsumerInfo(
            scan.ConsumerType,
            [..scan.MessageTypes],
            broker,
            scan.ToConsumerTopology(clientName, defaultPrefetchCount, overrideSettings)
        );
    }

    private static IConsumerTopology ToConsumerTopology(
        this ConsumerScanResult scan,
        string? clientName,
        ushort defaultPrefetchCount,
        ConsumerSettingsOptions? overrideSettings)
    {
        var prefetchCount = overrideSettings?.PrefetchCount ?? scan.QueueAttr?.PrefetchCount ?? defaultPrefetchCount;
        var queueName = overrideSettings?.QueueName ?? scan.QueueAttr?.Name ?? scan.ConsumerType.Name.ToConsumerQueueName(clientName);
        var maxRetries = overrideSettings?.MaxRetryAttempts ?? scan.QueueAttr?.MaxRetryAttempts;
        var dlx = overrideSettings?.DeadLetterExchange ?? scan.QueueAttr?.DeadLetterExchange;
        var dlRoutingKey = overrideSettings?.DeadLetterRoutingKey ?? scan.QueueAttr?.DeadLetterRoutingKey;
        var dlQueue = overrideSettings?.DeadLetterQueue ?? scan.QueueAttr?.DeadLetterQueue;

        var errorStrategy = scan.QueueAttr == null && overrideSettings == null
            ? ConsumerErrorStrategy.ByConvention(queueName)
            : new ConsumerErrorStrategy(
                maxRetries,
                scan.QueueAttr?.FailureAction ?? ConsumerFailureAction.DeadLetter,
                dlx,
                dlRoutingKey,
                dlQueue).WithConventionDefaults(queueName);

        if (scan.QueueAttr == null && overrideSettings?.RoutingKey == null)
        {
            return new ConsumerConventionTopology(
                Broker: overrideSettings?.Broker ?? string.Empty,
                Queue: queueName,
                ConsumerExchangeName: scan.ConsumerType.Name.ToConsumerExchangeName(clientName),
                MessageExchangeNames: scan.MessageTypes
                    .Select(m => m.Name.ToMessageExchangeName())
                    .ToList()
                    .AsReadOnly(),
                PrefetchCount: prefetchCount,
                ErrorStrategy: errorStrategy);
        }

        var routingKey = overrideSettings?.RoutingKey ?? scan.QueueAttr?.RoutingKey ?? string.Empty;
        var exchange = scan.QueueAttr?.Exchange ?? string.Empty;
        var bindings = scan.BindingAttrs
            .Select(b => new BindingInfo(
                b.Exchange,
                b.RoutingKey,
                b.ExchangeType,
                b.DeclareExchange,
                b.Durable,
                b.AutoDelete))
            .Union([new BindingInfo(
                exchange,
                routingKey,
                scan.QueueAttr?.ExchangeType ?? ExchangeType.Direct,
                scan.QueueAttr?.DeclareExchange ?? false,
                scan.QueueAttr?.ExchangeDurable ?? true,
                scan.QueueAttr?.ExchangeAutoDelete ?? false)])
            .ToList()
            .AsReadOnly();

        return new ConsumerAttributeTopology(
            Broker: overrideSettings?.Broker ?? scan.QueueAttr?.Broker ?? string.Empty,
            Queue: queueName,
            Bindings: bindings,
            PrefetchCount: prefetchCount,
            ErrorStrategy: errorStrategy,
            QueueDurable: scan.QueueAttr?.Durable ?? true,
            QueueExclusive: scan.QueueAttr?.Exclusive ?? false,
            QueueAutoDelete: scan.QueueAttr?.AutoDelete ?? false);
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
        string? clientName = null,
        Dictionary<string, ConsumerSettingsOptions>? consumerSettings = null)
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
                    var initialQueueName = sc.QueueAttr?.Name ?? sc.ConsumerType.Name.ToConsumerQueueName(clientName);
                    var overrideSetting = FindConsumerSettings(consumerSettings, sc.ConsumerType, initialQueueName);
                    var brokerName = overrideSetting?.Broker ?? sc.QueueAttr?.Broker ?? firstBrokerName;
                    var prefetchCount = brokers.GetValueOrDefault(brokerName)?.DefaultPrefetchCount ?? 1;
                    return sc.ToConsumerInfo(clientName, prefetchCount, overrideSetting) with { Broker = brokerName };
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
