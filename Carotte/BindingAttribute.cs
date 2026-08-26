namespace Carotte;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BindingAttribute(
    string exchange,
    string routingKey = "",
    ExchangeType exchangeType = ExchangeType.Direct,
    bool declareExchange = true,
    bool durable = true,
    bool autoDelete = false) : Attribute
{
    public string Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey;
    public ExchangeType ExchangeType { get; } = exchangeType;
    public bool DeclareExchange { get; } = declareExchange;
    public bool Durable { get; } = durable;
    public bool AutoDelete { get; } = autoDelete;
}
