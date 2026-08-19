using Shouldly;

namespace Carotte.Documentation.Tests;

public class ConsumerDocumenterTests
{
    private readonly ConsumerDocumenter _documenter = new();

    [Fact]
    public void Generate_WhenConsumersEmpty_ShouldReturnNoConsumersMessage()
    {
        // Act
        var result = _documenter.Generate([]);

        // Assert
        result.ShouldContain("No consumed messages configured");
    }

    [Fact]
    public void Generate_WithConsumer_ShouldIncludeTableHeader()
    {
        // Arrange
        var topology = new ConsumerAttributeTopology(
            "primary-broker",
            "orders-queue",
            [new BindingInfo("orders.exchange", "order.created", ExchangeType.Topic)],
            5,
            new ConsumerErrorStrategy(5, ConsumerFailureAction.DeadLetter, "orders.dlx", "orders-queue", "orders.dlq"));

        var consumer = new ConsumerInfo(typeof(OrderCreatedConsumer), [typeof(OrderCreatedMessage)], "primary-broker", topology);

        // Act
        var result = _documenter.Generate([consumer]);

        // Assert
        result.ShouldContain("| Message | Consumer | Queue | Broker | Bindings | Error Strategy |");
    }

    [Fact]
    public void Generate_WithConsumer_ShouldIncludeMessageName()
    {
        // Arrange
        var topology = new ConsumerAttributeTopology(
            "primary-broker",
            "orders-queue",
            [new BindingInfo("orders.exchange", "order.created", ExchangeType.Topic)],
            5,
            new ConsumerErrorStrategy(5, ConsumerFailureAction.DeadLetter, "orders.dlx", "orders-queue", "orders.dlq"));

        var consumer = new ConsumerInfo(typeof(OrderCreatedConsumer), [typeof(OrderCreatedMessage)], "primary-broker", topology);

        // Act
        var result = _documenter.Generate([consumer]);

        // Assert
        result.ShouldContain("`OrderCreatedMessage`");
    }

    [Fact]
    public void Generate_WithConsumer_ShouldIncludeQueueAndBroker()
    {
        // Arrange
        var topology = new ConsumerAttributeTopology(
            "primary-broker",
            "orders-queue",
            [new BindingInfo("orders.exchange", "order.created", ExchangeType.Topic)],
            5,
            new ConsumerErrorStrategy(5, ConsumerFailureAction.DeadLetter, "orders.dlx", "orders-queue", "orders.dlq"));

        var consumer = new ConsumerInfo(typeof(OrderCreatedConsumer), [typeof(OrderCreatedMessage)], "primary-broker", topology);

        // Act
        var result = _documenter.Generate([consumer]);

        // Assert
        result.ShouldContain("`orders-queue`");
    }

    [Fact]
    public void Generate_WithConsumer_ShouldIncludeErrorStrategyDetails()
    {
        // Arrange
        var topology = new ConsumerAttributeTopology(
            "primary-broker",
            "orders-queue",
            [new BindingInfo("orders.exchange", "order.created", ExchangeType.Topic)],
            5,
            new ConsumerErrorStrategy(5, ConsumerFailureAction.DeadLetter, "orders.dlx", "orders-queue", "orders.dlq"));

        var consumer = new ConsumerInfo(typeof(OrderCreatedConsumer), [typeof(OrderCreatedMessage)], "primary-broker", topology);

        // Act
        var result = _documenter.Generate([consumer]);

        // Assert
        result.ShouldContain("5 retries");
    }
}
