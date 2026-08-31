namespace Carotte;

public record CarotteOptions
{
    public string? ServiceName { get; set; }

    public Dictionary<string, RabbitMqOptions> Brokers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ConsumerSettingsOptions> Consumers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PublisherSettingsOptions> Publishers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CarotteSerializationOptions? Serialization { get; set; }
}

public record PublisherSettingsOptions
{
    public string? Broker { get; set; }
    public string? Exchange { get; set; }
    public string? RoutingKey { get; set; }
    public ExchangeType? ExchangeType { get; set; }
    public bool? DeclareExchange { get; set; }
    public bool? ExchangeDurable { get; set; }
    public bool? ExchangeAutoDelete { get; set; }
}

public record ConsumerSettingsOptions
{
    public ushort? PrefetchCount { get; set; }
    public int? MaxRetryAttempts { get; set; }
    public TimeSpan? InitialRetryInterval { get; set; }
    public double? RetryBackoffMultiplier { get; set; }
    public string? QueueName { get; set; }
    public string? RoutingKey { get; set; }
    public string? Broker { get; set; }
    public string? DeadLetterExchange { get; set; }
    public string? DeadLetterRoutingKey { get; set; }
    public string? DeadLetterQueue { get; set; }
    public ConsumerFailureAction? FailureAction { get; set; }
    public bool? QueueDurable { get; set; }
    public bool? QueueExclusive { get; set; }
    public bool? QueueAutoDelete { get; set; }
    public string? QueueType { get; set; }
    public Dictionary<string, object> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public record CarotteSerializationOptions
{
    public System.Text.Json.JsonSerializerOptions? JsonSerializerOptions { get; init; }
}
