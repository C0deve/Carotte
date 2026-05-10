namespace Carotte;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false)]
public class PublisherAttribute(string broker = "default", string? exchange = null) : Attribute
{
    public string Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
}
