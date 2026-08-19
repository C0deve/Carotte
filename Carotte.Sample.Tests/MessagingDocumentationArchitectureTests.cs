using System.Reflection;
using Carotte.Documentation;
using Shouldly;

namespace Carotte.Sample.Tests;

/// <summary>
/// Architecture and snapshot tests for living messaging documentation (Documentation-as-Code).
/// Ensures that the version-controlled documentation is always synchronized with the microservice topology and contracts.
/// </summary>
public sealed class MessagingDocumentationArchitectureTests
{
    private static readonly Assembly s_sampleAssembly = typeof(Program).Assembly;

    [Fact]
    public void DocumentationFile_ShouldExistOnDisk()
    {
        var docPath = GetDocumentationFilePath();

        File.Exists(docPath).ShouldBeTrue($"Expected documentation file to exist at '{docPath}'.");
    }

    [Fact]
    public async Task GeneratedDocumentation_ShouldMatch_CommittedSnapshot()
    {
        var docPath = GetDocumentationFilePath();
        var expectedMarkdown = await File.ReadAllTextAsync(docPath);

        var generator = new CarotteDocGenerator();
        var options = new CarotteDocumentationOptions
        {
            Title = "Carotte.Sample Messaging Specification"
        };

        var generatedMarkdown = generator.Generate(s_sampleAssembly, options);

        var normalizedGenerated = generatedMarkdown.Replace("\r\n", "\n").Trim();
        var normalizedExpected = expectedMarkdown.Replace("\r\n", "\n").Trim();

        normalizedGenerated.ShouldBe(
            normalizedExpected,
            "The messaging documentation snapshot is out of sync with the codebase. Run Carotte.DocCli or update docs/MESSAGING.md.");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_MermaidTopologyDiagram()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(s_sampleAssembly);

        markdown.ShouldContain("```mermaid");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_MultiMessageConsumer()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(s_sampleAssembly);

        markdown.ShouldContain("MultiMessageConsumer");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_OrderConsumer()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(s_sampleAssembly);

        markdown.ShouldContain("OrderConsumer");
    }

    private static string GetDocumentationFilePath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Carotte.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            throw new InvalidOperationException("Could not find solution root directory containing Carotte.slnx.");
        }

        return Path.Combine(currentDir.FullName, "Carotte.Sample", "docs", "MESSAGING.md");
    }
}
