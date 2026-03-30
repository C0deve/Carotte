namespace Carotte;

public abstract class Producer
{
    public string Broker { get; protected set; } = string.Empty;
    public string Exchange { get; protected set; } = string.Empty;
}
