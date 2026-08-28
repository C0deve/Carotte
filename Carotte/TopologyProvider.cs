using System.Collections.ObjectModel;

namespace Carotte;

internal static class TopologyProvider
{
    private static ConsumerSettingsOptions? FindConsumerSettings(
        this Dictionary<string, ConsumerSettingsOptions> settings,
        Type consumerType,
        string? queueName)
    {
        if (settings.TryGetValue(consumerType.Name, out var byName)) return byName;
        if (consumerType.FullName != null && settings.TryGetValue(consumerType.FullName, out var byFullName)) return byFullName;
        if (queueName != null && settings.TryGetValue(queueName, out var byQueue)) return byQueue;
        return null;
    }

    extension(ConsumerScanResult scan)
    {
        private ConsumerInfo ToConsumerInfo(string? clientName,
            ushort defaultPrefetchCount,
            ConsumerSettingsOptions? overrideSettings,
            string brokerName)
        {
            return new ConsumerInfo(
                scan.ConsumerType,
                [.. scan.MessageTypes],
                brokerName,
                scan.ToConsumerTopology(clientName, defaultPrefetchCount, overrideSettings, brokerName)
            );
        }

        private IConsumerTopology ToConsumerTopology(string? clientName,
            ushort defaultPrefetchCount,
            ConsumerSettingsOptions? overrideSettings,
            string brokerName)
        {
            var prefetchCount = overrideSettings?.PrefetchCount ?? scan.QueueAttr?.PrefetchCount ?? defaultPrefetchCount;
            var queueName = overrideSettings?.QueueName ?? scan.QueueAttr?.Name ?? scan.ConsumerType.Name.ToConsumerQueueName(clientName);
            var maxRetries = overrideSettings?.MaxRetryAttempts ?? scan.QueueAttr?.MaxRetryAttempts;
            var failureAction = overrideSettings?.FailureAction ?? scan.QueueAttr?.FailureAction ?? ConsumerFailureAction.DeadLetter;
            var dlx = overrideSettings?.DeadLetterExchange ?? scan.QueueAttr?.DeadLetterExchange;
            var dlRoutingKey = overrideSettings?.DeadLetterRoutingKey ?? scan.QueueAttr?.DeadLetterRoutingKey;
            var dlQueue = overrideSettings?.DeadLetterQueue ?? scan.QueueAttr?.DeadLetterQueue;
            var initialInterval = overrideSettings?.InitialRetryInterval;
            var backoffMultiplier = overrideSettings?.RetryBackoffMultiplier;

            var errorStrategy = scan.QueueAttr == null && overrideSettings == null
                ? ConsumerErrorStrategy.ByConvention(queueName)
                : new ConsumerErrorStrategy(
                    maxRetries,
                    failureAction,
                    dlx,
                    dlRoutingKey,
                    dlQueue,
                    InitialRetryInterval: initialInterval,
                    RetryBackoffMultiplier: backoffMultiplier).WithConventionDefaults(queueName);

            var readonlyArguments = overrideSettings is null
                ? ReadOnlyDictionary<string, object>.Empty
                : ConsumerScanResult.BuildConsumerArguments(overrideSettings);

            if (scan.QueueAttr == null && overrideSettings?.RoutingKey == null)
            {
                return new ConsumerConventionTopology(
                    Broker: brokerName,
                    Queue: queueName,
                    ConsumerExchangeName: scan.ConsumerType.Name.ToConsumerExchangeName(clientName),
                    MessageExchangeNames: scan.MessageTypes
                        .Select(m => m.Name.ToMessageExchangeName())
                        .ToList()
                        .AsReadOnly(),
                    Arguments: readonlyArguments,
                    PrefetchCount: prefetchCount,
                    ErrorStrategy: errorStrategy);
            }

            var explicitRoutingKey = overrideSettings?.RoutingKey ?? scan.QueueAttr?.RoutingKey;
            var exchange = scan.QueueAttr?.Exchange ?? string.Empty;

            var queueBindings = scan.GenerateQueueBindings(explicitRoutingKey, exchange);

            var bindings = scan.BindingAttrs
                .Select(b => new BindingInfo(
                    b.Exchange,
                    b.RoutingKey,
                    b.ExchangeType,
                    b.DeclareExchange,
                    b.Durable,
                    b.AutoDelete))
                .Union(queueBindings)
                .ToList()
                .AsReadOnly();

            return new ConsumerAttributeTopology(
                Broker: brokerName,
                Queue: queueName,
                Bindings: bindings,
                Arguments: readonlyArguments, PrefetchCount: prefetchCount, ErrorStrategy: errorStrategy, QueueDurable: overrideSettings?.QueueDurable ?? scan.QueueAttr?.Durable ?? true, QueueExclusive: overrideSettings?.QueueExclusive ?? scan.QueueAttr?.Exclusive ?? false, QueueAutoDelete: overrideSettings?.QueueAutoDelete ?? scan.QueueAttr?.AutoDelete ?? false);
        }

        private static ReadOnlyDictionary<string, object> BuildConsumerArguments(ConsumerSettingsOptions overrideSettings)
        {
            var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(overrideSettings.QueueType))
            {
                arguments["x-queue-type"] = overrideSettings.QueueType;
            }

            foreach (var (k, v) in overrideSettings.Arguments)
            {
                arguments[k] = v;
            }

            return new ReadOnlyDictionary<string, object>(arguments);
        }

        private IEnumerable<BindingInfo> GenerateQueueBindings(string? explicitRoutingKey, string exchange)
        {
            IEnumerable<BindingInfo> queueBindings;
            if (explicitRoutingKey != null)
            {
                queueBindings = [new BindingInfo(
                    exchange,
                    explicitRoutingKey,
                    scan.QueueAttr?.ExchangeType ?? ExchangeType.Direct,
                    scan.QueueAttr?.DeclareExchange ?? true,
                    scan.QueueAttr?.ExchangeDurable ?? true,
                    scan.QueueAttr?.ExchangeAutoDelete ?? false)];
            }
            else if (!string.IsNullOrWhiteSpace(exchange) && scan.MessageTypes.Count > 0)
            {
                queueBindings = scan.MessageTypes.Select(m => new BindingInfo(
                    exchange,
                    m.Name,
                    scan.QueueAttr?.ExchangeType ?? ExchangeType.Direct,
                    scan.QueueAttr?.DeclareExchange ?? true,
                    scan.QueueAttr?.ExchangeDurable ?? true,
                    scan.QueueAttr?.ExchangeAutoDelete ?? false));
            }
            else
            {
                queueBindings = [new BindingInfo(
                    exchange,
                    string.Empty,
                    scan.QueueAttr?.ExchangeType ?? ExchangeType.Direct,
                    scan.QueueAttr?.DeclareExchange ?? true,
                    scan.QueueAttr?.ExchangeDurable ?? true,
                    scan.QueueAttr?.ExchangeAutoDelete ?? false)];
            }

            return queueBindings;
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
                    var overrideSetting = consumerSettings?.FindConsumerSettings(sc.ConsumerType, initialQueueName);
                    var brokerName = overrideSetting?.Broker ?? sc.QueueAttr?.Broker ?? firstBrokerName;
                    var prefetchCount = brokers.GetValueOrDefault(brokerName)?.DefaultPrefetchCount ?? 1;
                    return sc.ToConsumerInfo(clientName, prefetchCount, overrideSetting, brokerName);
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
