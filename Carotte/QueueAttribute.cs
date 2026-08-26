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
    int maxRetryAttempts = -1,
    ConsumerFailureAction failureAction = ConsumerFailureAction.DeadLetter,
    string? deadLetterExchange = null,
    string? deadLetterRoutingKey = null,
    string? deadLetterQueue = null,
    bool durable = true,
    bool exclusive = false,
    bool autoDelete = false,
    ExchangeType exchangeType = ExchangeType.Direct,
    bool declareExchange = true,
    bool exchangeDurable = true,
    bool exchangeAutoDelete = false) : Attribute
{
    public string Name { get; } = name;
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey ?? string.Empty;
    public ushort PrefetchCount { get; } = prefetchCount;
    public int? MaxRetryAttempts { get; } = maxRetryAttempts < 0 ? null : maxRetryAttempts;
    public ConsumerFailureAction FailureAction { get; } = failureAction;
    public string? DeadLetterExchange { get; } = deadLetterExchange;
    public string? DeadLetterRoutingKey { get; } = deadLetterRoutingKey;
    public string? DeadLetterQueue { get; } = deadLetterQueue;
    public bool Durable { get; } = durable;
    public bool Exclusive { get; } = exclusive;
    public bool AutoDelete { get; } = autoDelete;
    public ExchangeType ExchangeType { get; } = exchangeType;
    public bool DeclareExchange { get; } = declareExchange;
    public bool ExchangeDurable { get; } = exchangeDurable;
    public bool ExchangeAutoDelete { get; } = exchangeAutoDelete;
}
