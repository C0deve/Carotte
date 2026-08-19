using Moq;
using Shouldly;

namespace Carotte.Documentation.Tests;

public class DataContractDocumenterTests
{
    private readonly DataContractDocumenter _documenter = new();

    [Fact]
    public void Generate_WhenMessageTypesEmpty_ShouldReturnNoContractsMessage()
    {
        // Act
        var result = _documenter.Generate([]);

        // Assert
        result.ShouldContain("No data contracts found");
    }

    [Fact]
    public void Generate_WithMessage_ShouldIncludeTypeHeading()
    {
        // Act
        var result = _documenter.Generate([typeof(OrderCreatedMessage)]);

        // Assert
        result.ShouldContain("#### `OrderCreatedMessage`");
    }

    [Fact]
    public void Generate_WithMessage_ShouldIncludePropertiesTable()
    {
        // Act
        var result = _documenter.Generate([typeof(OrderCreatedMessage)]);

        // Assert
        result.ShouldContain("| `OrderId` | `Guid` |");
    }

    [Fact]
    public void Generate_WithXmlReader_ShouldIncludePropertySummary()
    {
        // Arrange
        var mockXmlReader = new Mock<IXmlDocumentationReader>();
        mockXmlReader
            .Setup(r => r.GetPropertySummary(typeof(OrderCreatedMessage), nameof(OrderCreatedMessage.OrderId)))
            .Returns("Unique order ID");

        // Act
        var result = _documenter.Generate([typeof(OrderCreatedMessage)], mockXmlReader.Object);

        // Assert
        result.ShouldContain("Unique order ID");
    }

    [Fact]
    public void Generate_WithXmlReader_ShouldIncludeTypeSummary()
    {
        // Arrange
        var mockXmlReader = new Mock<IXmlDocumentationReader>();
        mockXmlReader
            .Setup(r => r.GetTypeSummary(typeof(OrderCreatedMessage)))
            .Returns("Order created event description");

        // Act
        var result = _documenter.Generate([typeof(OrderCreatedMessage)], mockXmlReader.Object);

        // Assert
        result.ShouldContain("Order created event description");
    }
}
