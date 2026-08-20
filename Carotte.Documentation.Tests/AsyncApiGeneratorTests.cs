using Shouldly;
using Carotte.Documentation.AsyncApi;

namespace Carotte.Documentation.Tests;

public class AsyncApiGeneratorTests
{
    private readonly AsyncApiGenerator _generator = new();

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeDocumentTitle()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Title = "Order Service AsyncAPI",
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("\"title\": \"Order Service AsyncAPI\"");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeAsyncApiVersion()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("\"asyncapi\": \"3.1.0\"");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeServer()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("primary-broker");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludePublishChannel()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("orders.exchange/order.created");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeComponentsSchemas()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("OrderCreatedMessage");
    }

    [Fact]
    public void Generate_WithYamlFormat_ShouldProduceYaml()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Yaml
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("asyncapi: 3.1.0");
    }

    [Fact]
    public void Generate_FromAssembly_ShouldIncludeOperations()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var options = new CarotteAsyncApiOptions
        {
            Format = AsyncApiFormat.Json
        };

        // Act
        var spec = _generator.Generate(assembly, options);

        // Assert
        spec.ShouldContain("\"publishOrderCreatedMessage\"");
        spec.ShouldContain("\"consumeOrderCreatedConsumer\"");
    }

    [Fact]
    public async Task GenerateToFileAsync_ShouldWriteFileSuccessfully()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var tempFile = Path.Combine(Path.GetTempPath(), $"asyncapi_test_{Guid.NewGuid():N}.yaml");

        try
        {
            // Act
            await _generator.GenerateToFileAsync(assembly, tempFile);

            // Assert
            File.Exists(tempFile).ShouldBeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.ShouldContain("asyncapi: 3.1.0");
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
