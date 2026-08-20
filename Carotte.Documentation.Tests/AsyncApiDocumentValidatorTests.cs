using Carotte.Documentation.AsyncApi;
using Shouldly;

namespace Carotte.Documentation.Tests;

public class AsyncApiDocumentValidatorTests
{
    private readonly AsyncApiDocumentValidator _validator = new();
    private readonly AsyncApiGenerator _generator = new();

    [Fact]
    public void Validate_GeneratedDocumentFromAssembly_ShouldBeValid()
    {
        // Arrange
        var assembly = typeof(OrderCreatedConsumer).Assembly;
        var yaml = _generator.Generate(assembly, new CarotteAsyncApiOptions { Format = AsyncApiFormat.Yaml });

        // Act
        var result = _validator.Validate(yaml);

        // Assert
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors));
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EmptyContent_ShouldReturnInvalid()
    {
        // Act
        var result = _validator.Validate("");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Content is empty.");
    }

    [Fact]
    public void Validate_InvalidYaml_ShouldReturnErrors()
    {
        // Arrange
        var invalidSpec = """
                          asyncapi: 3.1.0
                          info:
                            title: Invalid Spec
                          servers: invalid_type
                          """;

        // Act
        var result = _validator.Validate(invalidSpec);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }
}
