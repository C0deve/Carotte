namespace Carotte;

[AttributeUsage(AttributeTargets.Class)]
public class QueueAttribute(string name, string? broker = null, string? exchange = null, string? routingKey = "", ushort prefetchCount = 1) : Attribute
{
    public string Name { get; } = name;
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey ?? string.Empty;
    public ushort PrefetchCount { get; } = prefetchCount;
}
