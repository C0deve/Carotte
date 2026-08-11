namespace Carotte;

public enum ConsumerFailureAction
{
    DeadLetter = 0,
    Requeue = 1
}

[AttributeUsage(AttributeTargets.Class)]
public class QueueAttribute(
    string name,
    string? broker = null,
    string? exchange = null,
    string? routingKey = "",
    ushort prefetchCount = 1,
    int maxRetryAttempts = 0,
    ConsumerFailureAction failureAction = ConsumerFailureAction.DeadLetter,
    string? deadLetterExchange = null,
    string? deadLetterRoutingKey = null) : Attribute
{
    public string Name { get; } = name;
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey ?? string.Empty;
    public ushort PrefetchCount { get; } = prefetchCount;
    public int MaxRetryAttempts { get; } = maxRetryAttempts;
    public ConsumerFailureAction FailureAction { get; } = failureAction;
    public string? DeadLetterExchange { get; } = deadLetterExchange;
    public string? DeadLetterRoutingKey { get; } = deadLetterRoutingKey;
}
