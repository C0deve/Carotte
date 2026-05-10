namespace Carotte;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class PublisherAttribute(string broker = "default", string? exchange = null) : Attribute
{
    public string Broker { get; } = broker;
    public string? Exchange { get; } = exchange;
}
