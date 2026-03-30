namespace Carotte;

public abstract class Producer
{
    protected string Broker { get; init; } = string.Empty;
    protected string Exchange { get; init; } = string.Empty;
}
