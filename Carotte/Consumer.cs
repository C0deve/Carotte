namespace Carotte;

public abstract class Consumer
{
    public string Broker { get; protected set; } = string.Empty;
    public string Queue { get; protected set; } = string.Empty;
}
