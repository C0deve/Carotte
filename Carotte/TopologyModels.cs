using System.Collections.ObjectModel;

namespace Carotte;

public interface IConsumerTopology
{
    string Queue { get; }
    string Broker { get; }
    ushort PrefetchCount { get; }
    ConsumerErrorStrategy ErrorStrategy { get; }
    IReadOnlyDictionary<string, object?> Arguments { get; }
}

public readonly record struct ConsumerErrorStrategy(
    int? MaxRetryAttempts = null,
    ConsumerFailureAction FailureAction = ConsumerFailureAction.DeadLetter,
    string? DeadLetterExchange = null,
    string? DeadLetterRoutingKey = null,
    string? DeadLetterQueue = null,
    TimeSpan? InitialRetryInterval = null,
    double? RetryBackoffMultiplier = null,
    TimeSpan? RetryMaxInterval = null,
    bool UseJitter = false)
{
    private const int DefaultMaxRetryAttempts = 3;

    public bool RequeueOnFailure => FailureAction == ConsumerFailureAction.Requeue;

    public int EffectiveMaxRetryAttempts => MaxRetryAttempts ?? DefaultMaxRetryAttempts;

    public TimeSpan GetRetryDelay(int attempt)
    {
        var initial = InitialRetryInterval ?? TimeSpan.Zero;
        if (initial <= TimeSpan.Zero || attempt <= 1)
        {
            return initial <= TimeSpan.Zero ? TimeSpan.Zero : initial;
        }

        var multiplier = RetryBackoffMultiplier is > 0 ? RetryBackoffMultiplier.Value : 1.0;
        var factor = Math.Pow(multiplier, attempt - 1);
        var delayMs = initial.TotalMilliseconds * factor;

        if (RetryMaxInterval.HasValue && delayMs > RetryMaxInterval.Value.TotalMilliseconds)
        {
            delayMs = RetryMaxInterval.Value.TotalMilliseconds;
        }

        if (UseJitter)
        {
            var jitter = (Random.Shared.NextDouble() * 0.2) - 0.1; // +/- 10%
            delayMs = Math.Max(0, delayMs * (1 + jitter));
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    public static ConsumerErrorStrategy ByConvention(string queueName) => new(
        MaxRetryAttempts: DefaultMaxRetryAttempts,
        FailureAction: ConsumerFailureAction.DeadLetter,
        DeadLetterExchange: queueName.ToDeadLetterExchangeName(),
        DeadLetterRoutingKey: queueName,
        DeadLetterQueue: queueName.ToDeadLetterQueueName());

    public ConsumerErrorStrategy WithConventionDefaults(string queueName) => this with
    {
        MaxRetryAttempts = MaxRetryAttempts ?? DefaultMaxRetryAttempts,
        DeadLetterExchange = string.IsNullOrWhiteSpace(DeadLetterExchange)
            ? queueName.ToDeadLetterExchangeName()
            : DeadLetterExchange,
        DeadLetterRoutingKey = string.IsNullOrWhiteSpace(DeadLetterRoutingKey)
            ? queueName
            : DeadLetterRoutingKey,
        DeadLetterQueue = string.IsNullOrWhiteSpace(DeadLetterQueue)
            ? queueName.ToDeadLetterQueueName()
            : DeadLetterQueue
    };
}

public record ConsumerConventionTopology(
    string Broker,
    string Queue,
    string ConsumerExchangeName,
    ReadOnlyCollection<string> MessageExchangeNames,
    ushort PrefetchCount = 1,
    ConsumerErrorStrategy ErrorStrategy = default,
    IReadOnlyDictionary<string, object?>? Arguments = null) : IConsumerTopology
{
    public IReadOnlyDictionary<string, object?> Arguments { get; } = Arguments ?? ReadOnlyDictionary<string, object?>.Empty;
}

public record ConsumerAttributeTopology(
    string Broker,
    string Queue,
    ReadOnlyCollection<BindingInfo> Bindings,
    ushort PrefetchCount = 1,
    ConsumerErrorStrategy ErrorStrategy = default,
    bool QueueDurable = true,
    bool QueueExclusive = false,
    bool QueueAutoDelete = false,
    IReadOnlyDictionary<string, object?>? Arguments = null) : IConsumerTopology
{
    public IReadOnlyDictionary<string, object?> Arguments { get; } = Arguments ?? ReadOnlyDictionary<string, object?>.Empty;
}

public readonly record struct ConsumerInfo(
    Type ConsumerType,
    HashSet<Type> MessageTypes,
    string Broker,
    IConsumerTopology Topology
);

public readonly record struct BrokerInfos(string Host, int Port, string UserName, string Password)
{
    public static BrokerInfos Default => new("localhost", 5672, "guest", "guest");
}

public readonly record struct BindingInfo(
    string ExchangeSource,
    string RoutingKey,
    ExchangeType ExchangeType = ExchangeType.Direct,
    bool DeclareExchange = false,
    bool Durable = true,
    bool AutoDelete = false);

internal readonly record struct ProducerInfo(
    Type MessageType,
    string Broker,
    string ExchangePublication,
    string RoutingKey,
    ExchangeType ExchangeType,
    bool DeclareExchange,
    bool Durable,
    bool AutoDelete);
