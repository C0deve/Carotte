namespace Carotte;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PublishedAttribute(
    string? broker = null,
    string? exchange = null,
    string? routingKey = null,
    ExchangeType exchangeType = ExchangeType.Direct,
    bool declareExchange = true,
    bool exchangeDurable = true,
    bool exchangeAutoDelete = false) : Attribute
{
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string? RoutingKey { get; } = routingKey;
    public ExchangeType ExchangeType { get; } = exchangeType;
    public bool DeclareExchange { get; } = declareExchange;
    public bool ExchangeDurable { get; } = exchangeDurable;
    public bool ExchangeAutoDelete { get; } = exchangeAutoDelete;
}
