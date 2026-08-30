using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqPublisherTests
{
    [Published]
    public class TestMessage;

    [Fact]
    public async Task PublishAsync_ShouldRespectConvention_WhenExchangeIsNull()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var loggerMock = new Mock<ILogger<RabbitMqPublisher<TestMessage>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var broker = "test-broker";
        var message = new TestMessage();

        // Pass null or string.Empty for exchange to trigger convention
        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            null!);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        var expectedExchange = "x.pub.test";
        VerifyLog(loggerMock, LogLevel.Information, $"Starting RabbitMqPublisher for TestMessage on broker test-broker. Exchange: {expectedExchange}");

        // Verify exchange is declared as fanout (convention)
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify message is published to this exchange
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            expectedExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var loggerMock = new Mock<ILogger<RabbitMqPublisher<TestMessage>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var broker = "test-broker";
        var message = new TestMessage();

        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            null!);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        // ConnectAsync and ExchangeDeclareAsync should be called only once
        rabbitMqClient.Verify(c => c.ConnectAsync(broker, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldUseExplicitExchange_WhenProvided()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var loggerMock = new Mock<ILogger<RabbitMqPublisher<TestMessage>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var broker = "test-broker";
        var explicitExchange = "explicit-exchange";
        var message = new TestMessage();

        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            explicitExchange);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        VerifyLog(loggerMock, LogLevel.Information, "Starting RabbitMqPublisher for TestMessage on broker test-broker. Exchange: explicit-exchange");

        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            explicitExchange,
            "direct",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            explicitExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotDeclareExchange_WhenDeclareExchangeIsFalse()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var loggerMock = new Mock<ILogger<RabbitMqPublisher<TestMessage>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var broker = "test-broker";
        var explicitExchange = "explicit-exchange";
        var message = new TestMessage();

        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            explicitExchange,
            "routing.key",
            ExchangeType.Direct,
            declareExchange: false,
            durable: true,
            autoDelete: false);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object?>?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);

        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            explicitExchange,
            "routing.key",
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldUseConfiguredTopology()
    {
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            Mock.Of<ISerializer>(),
            Mock.Of<ILogger<RabbitMqPublisher<TestMessage>>>(),
            "test-broker",
            "orders",
            "order.created",
            ExchangeType.Topic,
            declareExchange: true,
            durable: false,
            autoDelete: true);

        await publisher.PublishAsync(new TestMessage());

        rabbitMqClient.Verify(client => client.ExchangeDeclareAsync(
            "orders",
            "topic",
            false,
            true,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(client => client.BasicPublishAsync<TestMessage>(
            "orders",
            "order.created",
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldUseInjectedMessageTypeResolver_WhenProvided()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var messageTypeResolver = new Mock<IMessageTypeResolver>();
        messageTypeResolver
            .Setup(r => r.GetTypeIdentifier(typeof(TestMessage)))
            .Returns("custom.identifier.test-message");

        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            Mock.Of<ILogger<RabbitMqPublisher<TestMessage>>>(),
            "test-broker",
            "test-exchange",
            "routing.key",
            ExchangeType.Direct,
            declareExchange: false,
            durable: true,
            autoDelete: false,
            messageTypeResolver.Object);

        // Act
        await publisher.PublishAsync(new TestMessage(), CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            "test-exchange",
            "routing.key",
            It.IsAny<byte[]>(),
            It.Is<BasicProperties>(p => p.Type == "custom.identifier.test-message"),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        messageTypeResolver.Verify(r => r.GetTypeIdentifier(typeof(TestMessage)), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldUseCustomResolverFromServiceCollection()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var customResolver = new Mock<IMessageTypeResolver>();
        customResolver
            .Setup(r => r.GetTypeIdentifier(typeof(TestMessage)))
            .Returns("custom.di.test-message");

        services.AddSingleton(rabbitMqClient.Object);
        services.AddSingleton(customResolver.Object);
        services.AddCarotte(c =>
        {
            c.AddBroker("test-broker", opt => opt.Host = "localhost");
            c.AddAssemblies(typeof(RabbitMqPublisherTests).Assembly);
        });

        await using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IPublisher<TestMessage>>();

        // Act
        await publisher.PublishAsync(new TestMessage(), CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.Is<BasicProperties>(p => p.Type == "custom.di.test-message"),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        customResolver.Verify(r => r.GetTypeIdentifier(typeof(TestMessage)), Times.Once);
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
}
