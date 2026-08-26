using System.Collections.ObjectModel;
using Moq;
using Shouldly;

namespace Carotte.Tests;

public class TopologyConfigurationTests
{
    [Fact]
    public async Task AttributeTopology_ShouldDeclareConfiguredExchangeAndQueue()
    {
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var topology = new ConsumerAttributeTopology(
            Broker: "broker",
            Queue: "orders-queue",
            Bindings:
            [
                new BindingInfo(
                    "orders",
                    "order.*",
                    ExchangeType.Topic,
                    DeclareExchange: true,
                    Durable: false,
                    AutoDelete: true)
            ],
            ErrorStrategy: new ConsumerErrorStrategy(FailureAction: ConsumerFailureAction.Requeue),
            QueueDurable: false,
            QueueExclusive: true,
            QueueAutoDelete: true);

        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient.Object, topology, CancellationToken.None);

        rabbitMqClient.Verify(client => client.ExchangeDeclareAsync(
            "orders",
            "topic",
            false,
            true,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(client => client.QueueDeclareAsync(
            "orders-queue",
            false,
            true,
            true,
            It.IsAny<IDictionary<string, object?>?>(),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(client => client.QueueBindAsync(
            "orders-queue",
            "orders",
            "order.*",
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TopologyProvider_ShouldPreservePublisherConfiguration()
    {
        var attribute = new PublisherAttribute(
            broker: "broker",
            exchange: "orders",
            routingKey: "order.created",
            exchangeType: ExchangeType.Topic,
            declareExchange: true,
            durable: false,
            autoDelete: true);

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult>().AsReadOnly(),
            new List<PublisherScanResult> { new(typeof(TestMessage), attribute) }.AsReadOnly());

        var producer = settings.Producers.Single();
        producer.ExchangePublication.ShouldBe("orders");
        producer.RoutingKey.ShouldBe("order.created");
        producer.ExchangeType.ShouldBe(ExchangeType.Topic);
        producer.DeclareExchange.ShouldBeTrue();
        producer.Durable.ShouldBeFalse();
        producer.AutoDelete.ShouldBeTrue();
    }

    [Fact]
    public void TopologyProvider_ShouldPreserveConsumerDeclarationConfiguration()
    {
        var queueAttribute = new QueueAttribute(
            "orders-queue",
            broker: "broker",
            exchange: "orders",
            routingKey: "order.*",
            durable: false,
            exclusive: true,
            autoDelete: true,
            exchangeType: ExchangeType.Topic,
            declareExchange: true,
            exchangeDurable: false,
            exchangeAutoDelete: true);
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttribute,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        topology.QueueDurable.ShouldBeFalse();
        topology.QueueExclusive.ShouldBeTrue();
        topology.QueueAutoDelete.ShouldBeTrue();
        var binding = topology.Bindings.Single();
        binding.ExchangeSource.ShouldBe("orders");
        binding.RoutingKey.ShouldBe("order.*");
        binding.ExchangeType.ShouldBe(ExchangeType.Topic);
        binding.DeclareExchange.ShouldBeTrue();
        binding.Durable.ShouldBeFalse();
        binding.AutoDelete.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ShouldRejectConflictingExchangeDeclarations()
    {
        var brokers = new ReadOnlyDictionary<string, BrokerInfos>(
            new Dictionary<string, BrokerInfos> { ["broker"] = BrokerInfos.Default });
        var producers = new List<ProducerInfo>
        {
            new(typeof(TestMessage), "broker", "orders", "", ExchangeType.Topic, true, true, false),
            new(typeof(OtherMessage), "broker", "orders", "", ExchangeType.Fanout, true, true, false)
        }.AsReadOnly();
        var settings = new MessageBrokerSettings(brokers, new List<ConsumerInfo>().AsReadOnly(), producers);

        var result = CarotteBuilderValidator.Validate(settings);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error is ConflictingExchangeDeclaration);
    }

    [Fact]
    public void TopologyProvider_ShouldUseDefaultBrokerForAttributeConsumer_WhenBrokerNotSpecified()
    {
        var queueAttribute = new QueueAttribute("orders-queue", exchange: "orders");
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttribute,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["default-broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var consumer = settings.Consumers.Single();
        consumer.Topology.Broker.ShouldBe("default-broker");
    }

    [Fact]
    public void TopologyProvider_ShouldUseDefaultBrokerForConventionConsumer_WhenBrokerNotSpecified()
    {
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            null,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["default-broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var consumer = settings.Consumers.Single();
        consumer.Topology.Broker.ShouldBe("default-broker");
    }

    private sealed class TestMessage;
    private sealed class OtherMessage;
    private sealed class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
