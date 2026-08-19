namespace Carotte.Documentation;

public sealed record CarotteDocumentationOptions
{
    public string Title { get; init; } = "Microservice Messaging Specification";
    public bool IncludeMermaidDiagram { get; init; } = true;
    public bool IncludeProducers { get; init; } = true;
    public bool IncludeConsumers { get; init; } = true;
    public bool IncludeDataContracts { get; init; } = true;
    public string? XmlDocumentationPath { get; init; }
    public IReadOnlyCollection<string> Namespaces { get; init; } = [];
    public string? ClientName { get; init; }
    public Dictionary<string, RabbitMqOptions>? Brokers { get; init; }
    public Dictionary<string, ConsumerSettingsOptions>? ConsumerSettings { get; init; }
}
