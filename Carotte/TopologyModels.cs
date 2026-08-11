
using System.Collections.ObjectModel;

namespace Carotte;

public interface IConsumerTopology
{
    string Queue { get; }
    string Broker { get; }
    ushort PrefetchCount { get; }
    ConsumerErrorStrategy ErrorStrategy { get; }
}

public readonly record struct ConsumerErrorStrategy(
    int? MaxRetryAttempts = null,
    ConsumerFailureAction FailureAction = ConsumerFailureAction.DeadLetter,
    string? DeadLetterExchange = null,
    string? DeadLetterRoutingKey = null,
    string? DeadLetterQueue = null)
{
    private const int DefaultMaxRetryAttempts = 3;

    public bool RequeueOnFailure => FailureAction == ConsumerFailureAction.Requeue;

    public int EffectiveMaxRetryAttempts => MaxRetryAttempts ?? DefaultMaxRetryAttempts;

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
    ConsumerErrorStrategy ErrorStrategy = default) : IConsumerTopology;

public record ConsumerAttributeTopology(
    string Broker,
    string Queue,
    ReadOnlyCollection<BindingInfo> Bindings,
    ushort PrefetchCount = 1,
    ConsumerErrorStrategy ErrorStrategy = default) : IConsumerTopology;

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
    string RoutingKey);

internal readonly record struct ProducerInfo(
    Type MessageType,
    string Broker,
    string ExchangePublication);

