namespace Carotte;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BindingAttribute(string exchange, string routingKey = "") : Attribute
{
    public string Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey;
}
