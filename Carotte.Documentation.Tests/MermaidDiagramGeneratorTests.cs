using System.Collections.ObjectModel;
using Shouldly;

namespace Carotte.Documentation.Tests;

public class MermaidDiagramGeneratorTests
{
    private readonly MermaidDiagramGenerator _generator = new();

    [Fact]
    public void Generate_WhenTopologyEmpty_ShouldReturnEmptyDiagramNotice()
    {
        // Arrange
        var settings = new MessageBrokerSettings(
            ReadOnlyDictionary<string, BrokerInfos>.Empty,
            ReadOnlyCollection<ConsumerInfo>.Empty,
            ReadOnlyCollection<ProducerInfo>.Empty);

        // Act
        var result = _generator.Generate(settings);

        // Assert
        result.ShouldContain("graph LR");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludePublisherNode()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);
        var settings = new MessageBrokerSettings(
            ReadOnlyDictionary<string, BrokerInfos>.Empty,
            ReadOnlyCollection<ConsumerInfo>.Empty,
            [producer]);

        // Act
        var result = _generator.Generate(settings);

        // Assert
        result.ShouldContain("OrderCreatedMessage_Publisher");
    }

    [Fact]
    public void Generate_WithProducer_ShouldIncludeExchangeNode()
    {
        // Arrange
        var producer = new ProducerInfo(typeof(OrderCreatedMessage), "primary-broker", "orders.exchange", "order.created", ExchangeType.Topic, true, true, false);
        var settings = new MessageBrokerSettings(
            ReadOnlyDictionary<string, BrokerInfos>.Empty,
            ReadOnlyCollection<ConsumerInfo>.Empty,
            [producer]);

        // Act
        var result = _generator.Generate(settings);

        // Assert
        result.ShouldContain("[(\"orders.exchange\")]");
    }

    [Fact]
    public void Generate_WithConsumer_ShouldIncludeConsumerAndQueueNodes()
    {
        // Arrange
        var topology = new ConsumerAttributeTopology(
            "primary-broker",
            "orders-queue",
            [new BindingInfo("orders.exchange", "order.created", ExchangeType.Topic)],
            1);

        var consumer = new ConsumerInfo(typeof(OrderCreatedConsumer), [typeof(OrderCreatedMessage)], "primary-broker", topology);
        var settings = new MessageBrokerSettings(
            ReadOnlyDictionary<string, BrokerInfos>.Empty,
            [consumer],
            ReadOnlyCollection<ProducerInfo>.Empty);

        // Act
        var result = _generator.Generate(settings);

        // Assert
        result.ShouldContain("[[\"orders-queue\"]]");
    }
}
