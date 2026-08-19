using System.Text.Json.Serialization;

namespace Carotte.Documentation.AsyncApi;

public sealed record AsyncApiDocument
{
    [JsonPropertyName("asyncapi")]
    public string AsyncApi { get; init; } = "2.6.0";

    [JsonPropertyName("info")]
    public AsyncApiInfo Info { get; init; } = new();

    [JsonPropertyName("servers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiServer>? Servers { get; init; }

    [JsonPropertyName("channels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiChannel>? Channels { get; init; }

    [JsonPropertyName("operations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiOperationV3>? Operations { get; init; }

    [JsonPropertyName("components")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiComponents? Components { get; init; }
}

public sealed record AsyncApiInfo
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "Microservice Messaging API";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public sealed record AsyncApiServer
{
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonPropertyName("host")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Host { get; init; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "amqp";

    [JsonPropertyName("protocolVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProtocolVersion { get; init; } = "0.9.1";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public sealed record AsyncApiChannel
{
    [JsonPropertyName("address")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Address { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("bindings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiChannelBindings? Bindings { get; init; }

    [JsonPropertyName("publish")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiOperation? Publish { get; init; }

    [JsonPropertyName("subscribe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiOperation? Subscribe { get; init; }

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiMessageRef>? Messages { get; set; }
}

public sealed record AsyncApiOperation
{
    [JsonPropertyName("operationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperationId { get; init; }

    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("bindings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiOperationBindings? Bindings { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Message { get; init; }
}

public sealed record AsyncApiOperationV3
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = "send";

    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("channel")]
    public AsyncApiChannelRef Channel { get; init; } = new();

    [JsonPropertyName("bindings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiOperationBindings? Bindings { get; init; }

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AsyncApiMessageRef>? Messages { get; init; }
}

public sealed record AsyncApiChannelRef
{
    [JsonPropertyName("$ref")]
    public string Ref { get; init; } = "";
}

public sealed record AsyncApiMessageRef
{
    [JsonPropertyName("$ref")]
    public string Ref { get; init; } = "";
}

public sealed record AsyncApiSchemaRef
{
    [JsonPropertyName("$ref")]
    public string Ref { get; init; } = "";
}

public sealed record AsyncApiChannelBindings
{
    [JsonPropertyName("amqp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpChannelBinding? Amqp { get; init; }
}

public sealed record AsyncApiAmqpChannelBinding
{
    [JsonPropertyName("is")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Is { get; init; }

    [JsonPropertyName("exchange")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpExchangeBinding? Exchange { get; init; }

    [JsonPropertyName("queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpQueueBinding? Queue { get; init; }
}

public sealed record AsyncApiOperationBindings
{
    [JsonPropertyName("amqp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpOperationBinding? Amqp { get; init; }
}

public sealed record AsyncApiAmqpOperationBinding
{
    [JsonPropertyName("queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpQueueBinding? Queue { get; init; }

    [JsonPropertyName("exchange")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiAmqpExchangeBinding? Exchange { get; init; }
}

public sealed record AsyncApiAmqpExchangeBinding
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("durable")]
    public bool Durable { get; init; } = true;

    [JsonPropertyName("autoDelete")]
    public bool AutoDelete { get; init; } = false;
}

public sealed record AsyncApiAmqpQueueBinding
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("durable")]
    public bool Durable { get; init; } = true;

    [JsonPropertyName("exclusive")]
    public bool Exclusive { get; init; } = false;

    [JsonPropertyName("autoDelete")]
    public bool AutoDelete { get; init; } = false;
}

public sealed record AsyncApiComponents
{
    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiMessage>? Messages { get; init; }

    [JsonPropertyName("schemas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiSchema>? Schemas { get; init; }
}

public sealed record AsyncApiMessage
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Payload { get; init; }
}
