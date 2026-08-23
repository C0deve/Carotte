using System.Reflection;
using Carotte.Documentation;
using Carotte.Documentation.AsyncApi;
using Shouldly;

namespace Carotte.Sample.Tests;

/// <summary>
/// Architecture and snapshot tests for living messaging documentation (Documentation-as-Code).
/// Ensures that the version-controlled documentation is always synchronized with the microservice topology and contracts.
/// </summary>
public sealed class MessagingDocumentationArchitectureTests
{
    private static readonly Assembly SampleAssembly = typeof(Program).Assembly;

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

        var generatedMarkdown = generator.Generate(SampleAssembly, options);

        var normalizedGenerated = generatedMarkdown.Replace("\r\n", "\n").Trim();
        var normalizedExpected = expectedMarkdown.Replace("\r\n", "\n").Trim();

        normalizedGenerated.ShouldBe(
            normalizedExpected,
            "The messaging documentation snapshot is out of sync with the codebase. Run Carotte.DocCli or update docs/MESSAGING.md.");
    }

    [Fact]
    public void AsyncApiDocumentationFile_ShouldExistOnDisk()
    {
        var docPath = GetAsyncApiDocumentationFilePath();

        File.Exists(docPath).ShouldBeTrue($"Expected AsyncAPI documentation file to exist at '{docPath}'.");
    }

    [Fact]
    public async Task GeneratedAsyncApiDocumentation_ShouldMatch_CommittedSnapshot()
    {
        var docPath = GetAsyncApiDocumentationFilePath();
        var expectedYaml = await File.ReadAllTextAsync(docPath);

        var generator = new AsyncApiGenerator();
        var options = new CarotteAsyncApiOptions
        {
            Title = "Carotte.Sample Messaging API"
        };

        var generatedYaml = generator.Generate(SampleAssembly, options);

        var normalizedGenerated = generatedYaml.Replace("\r\n", "\n").Trim();
        var normalizedExpected = expectedYaml.Replace("\r\n", "\n").Trim();

        normalizedGenerated.ShouldBe(
            normalizedExpected,
            "The AsyncAPI documentation snapshot is out of sync with the codebase. Run Carotte.DocCli or update docs/asyncapi.yaml.");
    }

    [Fact]
    public void GeneratedAsyncApiDocumentation_ShouldBeValidAsyncApi3_1()
    {
        var generator = new AsyncApiGenerator();
        var options = new CarotteAsyncApiOptions
        {
            Title = "Carotte.Sample Messaging API"
        };

        var generatedYaml = generator.Generate(SampleAssembly, options);

        var validator = new AsyncApiDocumentValidator();
        var result = validator.Validate(generatedYaml);

        result.IsValid.ShouldBeTrue($"AsyncAPI validation errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_MermaidTopologyDiagram()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(SampleAssembly);

        markdown.ShouldContain("```mermaid");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_OrderProcessingConsumer()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(SampleAssembly);

        markdown.ShouldContain("OrderProcessingConsumer");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_NotificationConsumer()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(SampleAssembly);

        markdown.ShouldContain("NotificationConsumer");
    }

    [Fact]
    public void GeneratedDocumentation_ShouldContain_OrderAuditConsumer()
    {
        var generator = new CarotteDocGenerator();

        var markdown = generator.Generate(SampleAssembly);

        markdown.ShouldContain("OrderAuditConsumer");
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

    private static string GetAsyncApiDocumentationFilePath()
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

        return Path.Combine(currentDir.FullName, "Carotte.Sample", "docs", "asyncapi.yaml");
    }
}
