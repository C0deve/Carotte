using Shouldly;

namespace Carotte.Documentation.Tests;

public class ProducerDocumenterTests
{
    private readonly ProducerDocumenter _documenter = new();

    [Fact]
    public void Generate_WhenProducersEmpty_ShouldReturnNoProducersMessage()
    {
        // Act
        var result = _documenter.Generate([]);

        // Assert
        result.ShouldContain("No produced messages configured");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludeTableHeader()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([producer]);

        // Assert
        result.ShouldContain("| Message | Broker | Exchange | Routing Key | Exchange Type |");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludeMessageName()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([producer]);

        // Assert
        result.ShouldContain("`OrderCreatedMessage`");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludeExchangeAndRoutingKey()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([producer]);

        // Assert
        result.ShouldContain("`orders.exchange`");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludeExchangeType()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);

        // Act
        var result = _documenter.Generate([producer]);

        // Assert
        result.ShouldContain("`Topic`");
    }
}
