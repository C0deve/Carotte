using Shouldly;

namespace Carotte.Documentation.Tests;

public class PublisherDocumenterTests
{
    private readonly PublisherDocumenter _documenter = new();

    [Fact]
    public void Generate_WhenPublishersEmpty_ShouldReturnNoPublishersMessage()
    {
        // Act
        var result = _documenter.Generate([]);

        // Assert
        result.ShouldContain("No produced messages configured");
    }

    [Fact]
    public void Generate_WithPublisher_ShouldIncludeTableHeader()
    {
        // Arrange
        var publisher = new PublisherInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([publisher]);

        // Assert
        result.ShouldContain("| Message | Broker | Exchange | Routing Key | Exchange Type |");
    }

    [Fact]
    public void Generate_WithPublisher_ShouldIncludeMessageName()
    {
        // Arrange
        var publisher = new PublisherInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([publisher]);

        // Assert
        result.ShouldContain("`OrderCreatedMessage`");
    }

    [Fact]
    public void Generate_WithPublisher_ShouldIncludeExchangeAndRoutingKey()
    {
        // Arrange
        var publisher = new PublisherInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([publisher]);

        // Assert
        result.ShouldContain("`orders.exchange`");
    }

    [Fact]
    public void Generate_WithPublisher_ShouldIncludeExchangeType()
    {
        // Arrange
        var publisher = new PublisherInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([publisher]);

        // Assert
        result.ShouldContain("`Topic`");
    }
}
