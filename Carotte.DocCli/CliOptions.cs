namespace Carotte.DocCli;

public sealed record CliOptions
{
    public string? AssemblyPath { get; init; }
    public string? OutputPath { get; init; }
    public string? Title { get; init; }
    public string? XmlDocPath { get; init; }
    public IReadOnlyCollection<string> Namespaces { get; init; } = [];
    public bool IncludeDiagram { get; init; } = true;
    public bool IncludeContracts { get; init; } = true;
    public string Format { get; init; } = "markdown";
    public string? ApiVersion { get; init; }
    public string? SpecVersion { get; init; }
    public bool ShowHelp { get; init; }
}
