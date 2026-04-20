using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqClientTests
{
    private readonly Mock<IConnectionManager> _connectionManagerMock = new();
    private readonly Mock<ILogger<RabbitMqClient>> _loggerMock = new();
    private readonly RabbitMqClient _client;

    public RabbitMqClientTests()
    {
        _loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _client = new RabbitMqClient(_connectionManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetChannelAsync_ShouldLogCreation()
    {
        // Arrange
        const string broker = "test-broker";
        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();
        _connectionManagerMock.Setup(m => m.GetConnectionAsync(broker)).ReturnsAsync(connectionMock.Object);
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(channelMock.Object);

        // Act
        await _client.GetChannelAsync(broker);

        // Assert
        VerifyLog(LogLevel.Information, "Creating new channel for broker test-broker");
    }

    [Fact]
    public async Task BasicPublishAsync_ShouldLogPublishing()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);

        // Act
        await _client.BasicPublishAsync<TestMessage>(
            broker: broker,
            exchange: "test-exchange",
            routingKey: "test-key",
            body: [],
            properties: new BasicProperties());

        // Assert
        VerifyLog(LogLevel.Debug, "Publishing message TestMessage to exchange test-exchange with routing key test-key on broker test-broker");
    }

    [Fact]
    public async Task BasicConsumeAsync_ShouldLogConsumption()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);

        // Act
        await _client.BasicConsumeAsync(
            broker: broker,
            queue: "test-queue",
            autoAck: false,
            consumerTag: "tag",
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: null!);

        // Assert
        VerifyLog(LogLevel.Information, "Starting consumption on queue test-queue for broker test-broker");
    }

    [Fact]
    public async Task QueueDeclareAsync_ShouldLogDeclaration()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);

        // Act
        await _client.QueueDeclareAsync(broker, "test-queue");

        // Assert
        VerifyLog(LogLevel.Information, "Declaring queue test-queue on broker test-broker");
    }

    [Fact]
    public async Task ExchangeDeclareAsync_ShouldLogDeclaration()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);

        // Act
        await _client.ExchangeDeclareAsync(broker, "test-exchange");

        // Assert
        VerifyLog(LogLevel.Information, "Declaring exchange test-exchange on broker test-broker");
    }

    [Fact]
    public async Task QueueBindAsync_ShouldLogBinding()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);

        // Act
        await _client.QueueBindAsync(broker, "test-queue", "test-exchange", "test-key");

        // Assert
        VerifyLog(LogLevel.Information, "Binding queue test-queue to exchange test-exchange with routing key test-key on broker test-broker");
    }

    [Fact]
    public async Task DisposeAsync_ShouldLogDisposing()
    {
        // Arrange
        const string broker = "test-broker";
        SetupBroker(broker);
        await _client.GetChannelAsync(broker);

        // Act
        await _client.DisposeAsync();

        // Assert
        VerifyLog(LogLevel.Information, "Disposing channel for broker test-broker");
    }

    private void SetupBroker(string broker)
    {
        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsOpen).Returns(true);
        _connectionManagerMock.Setup(m => m.GetConnectionAsync(broker)).ReturnsAsync(connectionMock.Object);
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(channelMock.Object);
    }

    private void VerifyLog(LogLevel level, string message) => _loggerMock.Verify(
        x => x.Log(
            level,
#pragma warning disable CA1873
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString() == message),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
#pragma warning restore CA1873
        Times.Once);

    // ReSharper disable once ClassNeverInstantiated.Global
    // ReSharper disable once MemberCanBePrivate.Global
    public class TestMessage;
}
