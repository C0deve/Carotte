namespace Carotte;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PublisherAttribute(
    string? broker = null,
    string? exchange = null,
    string? routingKey = null,
    ExchangeType exchangeType = ExchangeType.Direct,
    bool declareExchange = true,
    bool durable = true,
    bool autoDelete = false) : Attribute
{
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string? RoutingKey { get; } = routingKey;
    public ExchangeType ExchangeType { get; } = exchangeType;
    public bool DeclareExchange { get; } = declareExchange;
    public bool Durable { get; } = durable;
    public bool AutoDelete { get; } = autoDelete;
}
