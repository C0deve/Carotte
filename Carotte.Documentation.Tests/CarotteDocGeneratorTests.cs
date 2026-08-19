using Shouldly;

namespace Carotte.Documentation.Tests;

public class CarotteDocGeneratorTests
{
    private readonly CarotteDocGenerator _generator = new();

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeDocumentTitle()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteDocumentationOptions { Title = "Custom Test Service Messaging" };

        // Act
        var markdown = _generator.Generate(assembly, options);

        // Assert
        markdown.ShouldContain("# Custom Test Service Messaging");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeProducersSection()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;

        // Act
        var markdown = _generator.Generate(assembly);

        // Assert
        markdown.ShouldContain("### Produced Messages");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeConsumersSection()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;

        // Act
        var markdown = _generator.Generate(assembly);

        // Assert
        markdown.ShouldContain("### Consumed Messages");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeDataContractsSection()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;

        // Act
        var markdown = _generator.Generate(assembly);

        // Assert
        markdown.ShouldContain("### Data Contracts");
    }

    [Fact]
    public void Generate_WhenDiagramDisabled_ShouldNotIncludeMermaid()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteDocumentationOptions { IncludeMermaidDiagram = false };

        // Act
        var markdown = _generator.Generate(assembly, options);

        // Assert
        markdown.ShouldNotContain("```mermaid");
    }

    [Fact]
    public async Task GenerateToFileAsync_ShouldWriteFileSuccessfully()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var tempFile = Path.Combine(Path.GetTempPath(), $"messaging_test_{Guid.NewGuid():N}.md");

        try
        {
            // Act
            await _generator.GenerateToFileAsync(assembly, tempFile);

            // Assert
            File.Exists(tempFile).ShouldBeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.ShouldContain("### Produced Messages");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
