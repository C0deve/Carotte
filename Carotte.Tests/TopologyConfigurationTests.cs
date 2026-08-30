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
            Bindings: [
                new BindingInfo(
                    "orders",
                    "order.*",
                    ExchangeType.Topic,
                    DeclareExchange: true,
                    Durable: false,
                    AutoDelete: true)
            ], Arguments: ReadOnlyDictionary<string, object>.Empty,
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
    public async Task AttributeTopology_ShouldSkipBindingWithNoExchange()
    {
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var topology = new ConsumerAttributeTopology(
            Broker: "broker",
            Queue: "orders-queue",
            Bindings: [
                new BindingInfo("", "order.created"),
                new BindingInfo("orders", "order.created")
            ], Arguments: ReadOnlyDictionary<string, object>.Empty,
            ErrorStrategy: new ConsumerErrorStrategy(FailureAction: ConsumerFailureAction.Requeue));

        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient.Object, topology, CancellationToken.None);

        rabbitMqClient.Verify(client => client.QueueBindAsync(
            "orders-queue",
            "",
            "order.created",
            It.IsAny<IDictionary<string, object?>?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Never);
        rabbitMqClient.Verify(client => client.QueueBindAsync(
            "orders-queue",
            "orders",
            "order.created",
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TopologyProvider_ShouldPreservePublisherConfiguration()
    {
        var attribute = new PublishedAttribute(
            broker: "broker",
            exchange: "orders",
            routingKey: "order.created",
            exchangeType: ExchangeType.Topic,
            declareExchange: true,
            exchangeDurable: false,
            exchangeAutoDelete: true);

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

    [Fact]
    public void Attributes_ShouldHaveDeclareExchangeTrueByDefault()
    {
        var publisherAttr = new PublishedAttribute();
        publisherAttr.DeclareExchange.ShouldBeTrue();
        publisherAttr.ExchangeDurable.ShouldBeTrue();
        publisherAttr.ExchangeAutoDelete.ShouldBeFalse();

        var queueAttr = new QueueAttribute("test-queue");
        queueAttr.DeclareExchange.ShouldBeTrue();
        queueAttr.ExchangeDurable.ShouldBeTrue();
        queueAttr.ExchangeAutoDelete.ShouldBeFalse();

        var bindingAttr = new BindingAttribute("test-exchange");
        bindingAttr.DeclareExchange.ShouldBeTrue();
        bindingAttr.ExchangeDurable.ShouldBeTrue();
        bindingAttr.ExchangeAutoDelete.ShouldBeFalse();

        var bindingInfo = new BindingInfo("test-exchange", "test-key");
        bindingInfo.DeclareExchange.ShouldBeTrue();
    }

    [Fact]
    public void TopologyProvider_ShouldDefaultDeclareExchangeToTrue_WhenNotSpecified()
    {
        var publisherAttr = new PublishedAttribute(broker: "broker", exchange: "orders");
        var queueAttr = new QueueAttribute("orders-queue", broker: "broker", exchange: "orders");
        var bindingAttr = new BindingAttribute("other-exchange", "other.key");

        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttr,
            new List<BindingAttribute> { bindingAttr }.AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult> { new(typeof(TestMessage), publisherAttr) }.AsReadOnly());

        var producer = settings.Producers.Single();
        producer.DeclareExchange.ShouldBeTrue();

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        topology.Bindings.Count.ShouldBe(2);
        topology.Bindings[0].DeclareExchange.ShouldBeTrue(); // bindingAttr
        topology.Bindings[1].DeclareExchange.ShouldBeTrue(); // queueAttr exchange
    }

    [Fact]
    public void TopologyProvider_ShouldRespectDeclareExchangeFalse_WhenSpecified()
    {
        var publisherAttr = new PublishedAttribute(broker: "broker", exchange: "orders", declareExchange: false);
        var queueAttr = new QueueAttribute("orders-queue", broker: "broker", exchange: "orders", declareExchange: false);
        var bindingAttr = new BindingAttribute("other-exchange", "other.key", declareExchange: false);

        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttr,
            new List<BindingAttribute> { bindingAttr }.AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult> { new(typeof(TestMessage), publisherAttr) }.AsReadOnly());

        var producer = settings.Producers.Single();
        producer.DeclareExchange.ShouldBeFalse();

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        topology.Bindings.Count.ShouldBe(2);
        topology.Bindings[0].DeclareExchange.ShouldBeFalse();
        topology.Bindings[1].DeclareExchange.ShouldBeFalse();
    }

    [Fact]
    public async Task AttributeTopology_ShouldNotDeclareExchange_WhenDeclareExchangeIsFalse()
    {
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var topology = new ConsumerAttributeTopology(
            Broker: "broker",
            Queue: "orders-queue",
            Bindings: [
                new BindingInfo(
                    "orders",
                    "order.*",
                    ExchangeType.Topic,
                    DeclareExchange: false)
            ],
            Arguments: ReadOnlyDictionary<string, object>.Empty,
            ErrorStrategy: new ConsumerErrorStrategy(FailureAction: ConsumerFailureAction.Requeue));

        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient.Object, topology, CancellationToken.None);

        rabbitMqClient.Verify(client => client.ExchangeDeclareAsync(
            "orders",
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object?>?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);

        rabbitMqClient.Verify(client => client.QueueBindAsync(
            "orders-queue",
            "orders",
            "order.*",
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TopologyProvider_ShouldDefaultRoutingKeyToMessageType_WhenExchangeSpecifiedAndRoutingKeyNull()
    {
        var queueAttr = new QueueAttribute("orders-queue", broker: "broker", exchange: "orders");
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttr,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        var binding = topology.Bindings.Single();
        binding.ExchangeSource.ShouldBe("orders");
        binding.RoutingKey.ShouldBe(nameof(TestMessage));
    }

    [Fact]
    public void TopologyProvider_ShouldDefaultRoutingKeyToAllMessageTypes_WhenMultipleMessageTypesAndRoutingKeyNull()
    {
        var queueAttr = new QueueAttribute("multi-queue", broker: "broker", exchange: "orders");
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage), typeof(OtherMessage) }.AsReadOnly(),
            queueAttr,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        topology.Bindings.Count.ShouldBe(2);
        topology.Bindings.ShouldContain(b => b.ExchangeSource == "orders" && b.RoutingKey == nameof(TestMessage));
        topology.Bindings.ShouldContain(b => b.ExchangeSource == "orders" && b.RoutingKey == nameof(OtherMessage));
    }

    [Fact]
    public void TopologyProvider_ShouldUseExplicitEmptyRoutingKey_WhenEmptyStringSpecified()
    {
        var queueAttr = new QueueAttribute("orders-queue", broker: "broker", exchange: "orders", routingKey: "");
        var scanResult = new ConsumerScanResult(
            typeof(TestConsumer),
            new List<Type> { typeof(TestMessage) }.AsReadOnly(),
            queueAttr,
            new List<BindingAttribute>().AsReadOnly());

        var settings = TopologyProvider.CreateSettings(
            new Dictionary<string, RabbitMqOptions> { ["broker"] = new() },
            new List<ConsumerScanResult> { scanResult }.AsReadOnly(),
            new List<PublisherScanResult>().AsReadOnly());

        var topology = settings.Consumers.Single().Topology.ShouldBeOfType<ConsumerAttributeTopology>();
        var binding = topology.Bindings.Single();
        binding.ExchangeSource.ShouldBe("orders");
        binding.RoutingKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConsumerErrorStrategy_ShouldNotAddDeadLetterDefaults_WhenFailureActionIsRequeue()
    {
        var strategy = new ConsumerErrorStrategy(FailureAction: ConsumerFailureAction.Requeue);
        var withDefaults = strategy.WithConventionDefaults("test-queue");

        withDefaults.DeadLetterExchange.ShouldBeNull();
        withDefaults.DeadLetterRoutingKey.ShouldBeNull();
        withDefaults.DeadLetterQueue.ShouldBeNull();
    }

    [Fact]
    public async Task AttributeTopology_ShouldNotDeclareDeadLetterExchangeOrArguments_WhenFailureActionIsRequeue()
    {
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var topology = new ConsumerAttributeTopology(
            Broker: "broker",
            Queue: "orders-queue",
            Bindings: [],
            Arguments: ReadOnlyDictionary<string, object>.Empty,
            ErrorStrategy: new ConsumerErrorStrategy(FailureAction: ConsumerFailureAction.Requeue));

        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient.Object, topology, CancellationToken.None);

        rabbitMqClient.Verify(client => client.ExchangeDeclareAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object?>?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);

        rabbitMqClient.Verify(client => client.QueueDeclareAsync(
            "orders-queue",
            true,
            false,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestMessage;
    private sealed class OtherMessage;
    private sealed class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
