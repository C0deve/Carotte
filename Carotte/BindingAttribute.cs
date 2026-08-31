namespace Carotte;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BindingAttribute(
    string exchange,
    string routingKey = "",
    ExchangeType exchangeType = ExchangeType.Direct,
    bool declareExchange = true,
    bool exchangeDurable = true,
    bool exchangeAutoDelete = false) : Attribute
{
    public string Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey;
    public ExchangeType ExchangeType { get; } = exchangeType;
    public bool DeclareExchange { get; } = declareExchange;
    public bool ExchangeDurable { get; } = exchangeDurable;
    public bool ExchangeAutoDelete { get; } = exchangeAutoDelete;
}
