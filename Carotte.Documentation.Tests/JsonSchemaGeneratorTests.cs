using Moq;
using Shouldly;
using Carotte.Documentation.AsyncApi;

namespace Carotte.Documentation.Tests;

public class JsonSchemaGeneratorTests
{
    private readonly JsonSchemaGenerator _generator = new();

    public record SimplePrimitiveMessage(
        string Text,
        int Integer,
        long LongValue,
        double DoubleValue,
        decimal DecimalValue,
        bool IsActive,
        Guid Identifier,
        DateTime CreatedAt,
        DateTimeOffset ModifiedAt,
        TimeSpan Duration);

    public record NullablePrimitiveMessage(
        int? OptionalInt,
        string? OptionalText,
        DateTime? OptionalDate);

    public enum StatusEnum
    {
        Pending,
        Active,
        Completed
    }

    public record EnumMessage(StatusEnum Status);

    public record CollectionMessage(
        List<string> Tags,
        int[] Numbers,
        IReadOnlyCollection<Guid> Identifiers);

    public record NestedChild(string Name, int Value);

    public record NestedMessage(string Title, NestedChild Child, List<NestedChild> Children);

    [Fact]
    public void Generate_ForPrimitiveTypes_ShouldProduceCorrectSchemaTypes()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Type.ShouldBe("object");
    }

    [Fact]
    public void Generate_ForPrimitiveTypes_ShouldHaveCorrectPropertyCount()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Properties!.Count.ShouldBe(10);
    }

    [Fact]
    public void Generate_ForStringType_ShouldSetTypeString()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Properties!["Text"].Type.ShouldBe("string");
    }

    [Fact]
    public void Generate_ForIntType_ShouldSetTypeInteger()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Properties!["Integer"].Type.ShouldBe("integer");
    }

    [Fact]
    public void Generate_ForGuidType_ShouldSetTypeStringAndFormatUuid()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Properties!["Identifier"].Format.ShouldBe("uuid");
    }

    [Fact]
    public void Generate_ForDateTimeType_ShouldSetFormatDateTime()
    {
        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage));

        // Assert
        schema.Properties!["CreatedAt"].Format.ShouldBe("date-time");
    }

    [Fact]
    public void Generate_ForEnumType_ShouldIncludeEnumValues()
    {
        // Act
        var schema = _generator.Generate(typeof(EnumMessage));

        // Assert
        schema.Properties!["Status"].EnumValues.ShouldNotBeNull();
    }

    [Fact]
    public void Generate_ForArrayType_ShouldSetTypeArray()
    {
        // Act
        var schema = _generator.Generate(typeof(CollectionMessage));

        // Assert
        schema.Properties!["Tags"].Type.ShouldBe("array");
    }

    [Fact]
    public void Generate_ForArrayType_ShouldSetItemType()
    {
        // Act
        var schema = _generator.Generate(typeof(CollectionMessage));

        // Assert
        schema.Properties!["Tags"].Items?.Type.ShouldBe("string");
    }

    [Fact]
    public void Generate_ForNestedType_ShouldGenerateNestedObjectProperties()
    {
        // Act
        var schema = _generator.Generate(typeof(NestedMessage));

        // Assert
        schema.Properties!["Child"].Type.ShouldBe("object");
    }

    [Fact]
    public void Generate_WithXmlDocumentationReader_ShouldSetTypeDescription()
    {
        // Arrange
        var mockXmlReader = new Mock<IXmlDocumentationReader>();
        mockXmlReader.Setup(r => r.GetTypeSummary(typeof(SimplePrimitiveMessage)))
            .Returns("Summary for simple primitive message");

        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage), mockXmlReader.Object);

        // Assert
        schema.Description.ShouldBe("Summary for simple primitive message");
    }

    [Fact]
    public void Generate_WithXmlDocumentationReader_ShouldSetPropertyDescription()
    {
        // Arrange
        var mockXmlReader = new Mock<IXmlDocumentationReader>();
        mockXmlReader.Setup(r => r.GetPropertySummary(typeof(SimplePrimitiveMessage), "Text"))
            .Returns("Description for Text property");

        // Act
        var schema = _generator.Generate(typeof(SimplePrimitiveMessage), mockXmlReader.Object);

        // Assert
        schema.Properties!["Text"].Description.ShouldBe("Description for Text property");
    }
}
