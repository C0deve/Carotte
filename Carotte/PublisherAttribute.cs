namespace Carotte;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PublisherAttribute(string? broker = null, string? exchange = null) : Attribute
{
    public string? Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
}
