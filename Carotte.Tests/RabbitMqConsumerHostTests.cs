using Microsoft.Extensions.Logging;
using Moq;

namespace Carotte.Tests;

public class RabbitMqConsumerHostTests
{
    [Fact]
    public async Task StartAsync_ShouldInitializeAndSetupTopology()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [new BindingInfo("test-exchange", "test-key")]);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        VerifyLog(loggerMock, LogLevel.Information, "Starting RabbitMqConsumerHost for TestConsumer on broker test-broker. Queue: test-queue, Exchanges: test-exchange, Messages: 'TestMessage'");
        VerifyLog(loggerMock, LogLevel.Information, "Setting up topology for TestConsumer");

        rabbitMqClient.Verify(c => c.ConnectAsync(broker, It.IsAny<CancellationToken>()), Times.Once);

        // Check topology setup
        rabbitMqClient.Verify(c => c.QueueDeclareAsync("test-queue", true, false, false, It.IsAny<IDictionary<string, object?>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.QueueBindAsync("test-queue", "test-exchange", "test-key", It.IsAny<IDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()), Times.Once);

        // Check consumer setup
        rabbitMqClient.Verify(c => c.BasicConsumeAsync("test-queue", false, string.Empty, false, false, It.IsAny<IDictionary<string, object>?>(), It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
        rabbitMqClient.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupDefaultQos()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [],
            PrefetchCount: 1); // Default is now 1

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicQosAsync(0, 1, false, It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupQos()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [],
            PrefetchCount: 15);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicQosAsync(0, 15, false, It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupMultipleQueueAttributes()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        const string testQueue = "test-queue";
        const string testExchange = "test-exchange";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: testQueue,
            Bindings: [
                new BindingInfo(testExchange, "key1"),
                new BindingInfo(testExchange, "key2")
            ]);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        VerifyLog(loggerMock, LogLevel.Information, "Starting RabbitMqConsumerHost for TestConsumer on broker test-broker. Queue: test-queue, Exchanges: test-exchange, Messages: 'TestMessage'");
        VerifyLog(loggerMock, LogLevel.Information, "Setting up topology for TestConsumer");

        rabbitMqClient.Verify(c => c.QueueDeclareAsync(testQueue, true, false, false, It.IsAny<IDictionary<string, object?>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.QueueBindAsync(testQueue, testExchange, "key1", It.IsAny<IDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.QueueBindAsync(testQueue, testExchange, "key2", It.IsAny<IDictionary<string, object?>>(), false, It.IsAny<CancellationToken>()), Times.Once);

        // Should consume only once from "test-queue"
        rabbitMqClient.Verify(c => c.BasicConsumeAsync(testQueue, false, string.Empty, false, false, It.IsAny<IDictionary<string, object?>>(), It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldCloseAndDisposeRabbitMqClient()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            new ConsumerAttributeTopology(Broker: broker, Queue: "test-queue", Bindings: []));

        // Act
        await host.StopAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Queue("test-queue")]
    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static void VerifyLog<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string message)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == message),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public class TestMessage;
}