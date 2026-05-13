namespace Carotte;

[AttributeUsage(AttributeTargets.Class)]
public class QueueAttribute(string name, string? broker = null, string? exchange = null, string? routingKey = "") : Attribute
{
    public string Name { get; } = name;
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey ?? string.Empty;
}
