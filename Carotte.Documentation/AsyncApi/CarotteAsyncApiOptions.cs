namespace Carotte.Documentation.AsyncApi;

public sealed record CarotteAsyncApiOptions
{
    public string Title { get; init; } = "Microservice Messaging API";
    public string Version { get; init; } = "1.0.0";
    public string? Description { get; init; }
    public AsyncApiFormat Format { get; init; } = AsyncApiFormat.Yaml;
    public string? XmlDocumentationPath { get; init; }
    public IReadOnlyCollection<string> Namespaces { get; init; } = [];
    public string? ClientName { get; init; }
    public Dictionary<string, RabbitMqOptions>? Brokers { get; init; }
    public Dictionary<string, ConsumerSettingsOptions>? ConsumerSettings { get; init; }
}
