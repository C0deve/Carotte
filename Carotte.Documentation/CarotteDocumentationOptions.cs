namespace Carotte.Documentation;

public sealed record CarotteDocumentationOptions
{
    public string Title { get; init; } = "Microservice Messaging Specification";
    public bool IncludeMermaidDiagram { get; init; } = true;
    public bool IncludePublishers { get; init; } = true;

    [Obsolete("Use IncludePublishers instead.")]
    public bool IncludeProducers
    {
        get => IncludePublishers;
        init => IncludePublishers = value;
    }

    public bool IncludeConsumers { get; init; } = true;
    public bool IncludeDataContracts { get; init; } = true;
    public string? XmlDocumentationPath { get; init; }
    public IReadOnlyCollection<string> Namespaces { get; init; } = [];
    public string? ClientName { get; init; }
    public Dictionary<string, RabbitMqOptions>? Brokers { get; init; }
    public Dictionary<string, ConsumerSettingsOptions>? ConsumerSettings { get; init; }
}
