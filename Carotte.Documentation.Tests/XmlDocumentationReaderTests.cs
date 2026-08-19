using Shouldly;

namespace Carotte.Documentation.Tests;

public class XmlDocumentationReaderTests
{
    private const string SampleXml = """
        <?xml version="1.0"?>
        <doc>
            <assembly>
                <name>Carotte.Documentation.Tests</name>
            </assembly>
            <members>
                <member name="T:Carotte.Documentation.Tests.OrderCreatedMessage">
                    <summary>Represents an event triggered when an order is created.</summary>
                </member>
                <member name="P:Carotte.Documentation.Tests.OrderCreatedMessage.OrderId">
                    <summary>The unique order identifier.</summary>
                </member>
                <member name="P:Carotte.Documentation.Tests.OrderCreatedMessage.CustomerName">
                    <summary>The customer full name.</summary>
                </member>
            </members>
        </doc>
        """;

    [Fact]
    public void GetTypeSummary_WhenXmlLoaded_ShouldReturnTypeSummary()
    {
        // Arrange
        var reader = XmlDocumentationReader.FromXmlString(SampleXml);

        // Act
        var summary = reader.GetTypeSummary(typeof(OrderCreatedMessage));

        // Assert
        summary.ShouldBe("Represents an event triggered when an order is created.");
    }

    [Fact]
    public void GetPropertySummary_WhenXmlLoaded_ShouldReturnPropertySummary()
    {
        // Arrange
        var reader = XmlDocumentationReader.FromXmlString(SampleXml);

        // Act
        var summary = reader.GetPropertySummary(typeof(OrderCreatedMessage), nameof(OrderCreatedMessage.OrderId));

        // Assert
        summary.ShouldBe("The unique order identifier.");
    }

    [Fact]
    public void GetPropertySummary_WhenPropertyNotDocumented_ShouldReturnNull()
    {
        // Arrange
        var reader = XmlDocumentationReader.FromXmlString(SampleXml);

        // Act
        var summary = reader.GetPropertySummary(typeof(OrderCreatedMessage), nameof(OrderCreatedMessage.Amount));

        // Assert
        summary.ShouldBeNull();
    }
}
