namespace Carotte;

public enum ExchangeType
{
    Direct,
    Topic,
    Fanout,
    Headers
}

public record ExchangeOptions
{
    public string Name { get; set; } = string.Empty;
    public ExchangeType Type { get; set; } = ExchangeType.Direct;
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
}

public record QueueOptions
{
    public string Name { get; set; } = string.Empty;
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
}

public record BindingOptions
{
    public string QueueName { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
}
