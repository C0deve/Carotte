using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqClientTests
{
    private readonly Mock<IConnectionManager> _connectionManagerMock = new();
    private readonly Mock<ILogger<RabbitMqClient>> _loggerMock = new();
    private readonly RabbitMqClient _client;
    private readonly Mock<IChannel> _channelMock = new();

    private readonly Mock<IConnection> _connectionMock = new();

    public RabbitMqClientTests()
    {
        _loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _client = new RabbitMqClient(_connectionManagerMock.Object, _loggerMock.Object);
        _channelMock.Setup(c => c.IsOpen).Returns(true);
        _connectionManagerMock.Setup(m => m.GetConnectionAsync(It.IsAny<string>()))
            .ReturnsAsync(_connectionMock.Object);
        _connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);
    }

    private async Task InitializeClientAsync()
    {
        await _client.ConnectAsync("test-broker");
    }

    [Fact]
    public async Task InitializeAsync_ShouldRegisterHost()
    {
        // Act
        await _client.ConnectAsync("test-broker");

        // Assert
        _connectionManagerMock.Verify(m => m.RegisterHostAsync("test-broker"), Times.Once);
        _connectionManagerMock.Verify(m => m.GetConnectionAsync("test-broker"), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_ShouldNotRegisterHostAgain_WhenChannelIsOpen()
    {
        // Act
        await _client.ConnectAsync("test-broker");
        await _client.ConnectAsync("test-broker");

        // Assert
        _connectionManagerMock.Verify(m => m.RegisterHostAsync("test-broker"), Times.Once);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrow_WhenAlreadyConnectedToAnotherBroker()
    {
        // Act
        await _client.ConnectAsync("test-broker");

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.ConnectAsync("other-broker"));
    }

    [Fact]
    public async Task CloseAsync_ShouldUnregisterHost()
    {
        // Arrange
        await _client.ConnectAsync("test-broker");

        // Act
        await _client.CloseAsync();

        // Assert
        _connectionManagerMock.Verify(m => m.UnregisterHostAsync("test-broker"), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_ShouldClearStateAndAllowReconnect()
    {
        // Arrange
        var firstChannel = new Mock<IChannel>();
        var secondChannel = new Mock<IChannel>();
        firstChannel.Setup(c => c.IsOpen).Returns(true);
        secondChannel.Setup(c => c.IsOpen).Returns(true);
        _connectionMock
            .SetupSequence(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstChannel.Object)
            .ReturnsAsync(secondChannel.Object);

        await _client.ConnectAsync("test-broker");

        // Act
        await _client.CloseAsync();
        await _client.ConnectAsync("test-broker");

        // Assert
        firstChannel.Verify(c => c.DisposeAsync(), Times.Once);
        _connectionManagerMock.Verify(m => m.RegisterHostAsync("test-broker"), Times.Exactly(2));
        _connectionManagerMock.Verify(m => m.UnregisterHostAsync("test-broker"), Times.Once);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task QueueDeclareAsync_ShouldRecreateChannel_WhenExistingChannelIsClosed()
    {
        // Arrange
        var closedChannel = new Mock<IChannel>();
        var openChannel = new Mock<IChannel>();
        closedChannel.Setup(c => c.IsOpen).Returns(false);
        openChannel.Setup(c => c.IsOpen).Returns(true);
        _connectionMock
            .SetupSequence(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedChannel.Object)
            .ReturnsAsync(openChannel.Object);

        await _client.ConnectAsync("test-broker");

        // Act
        await _client.QueueDeclareAsync("test-queue");

        // Assert
        closedChannel.Verify(c => c.DisposeAsync(), Times.Once);
        openChannel.Verify(c => c.QueueDeclareAsync("test-queue", true, false, false, null, false, false, It.IsAny<CancellationToken>()), Times.Once);
        _connectionManagerMock.Verify(m => m.RegisterHostAsync("test-broker"), Times.Once);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BasicPublishAsync_ShouldLogPublishing()
    {
        // Arrange
        await InitializeClientAsync();
        const string exchange = "test-exchange";
        const string routingKey = "test-key";

        // Act
        await _client.BasicPublishAsync<TestMessage>(
            exchange: exchange,
            routingKey: routingKey,
            body: [],
            properties: new BasicProperties());

        // Assert
        VerifyLog(LogLevel.Debug, $"Publishing message TestMessage to exchange {exchange} with routing key {routingKey} on broker ");
        _channelMock.Verify(c => c.BasicPublishAsync(exchange, routingKey, true, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BasicConsumeAsync_ShouldLogConsumption()
    {
        // Arrange
        await InitializeClientAsync();
        const string queue = "test-queue";

        // Act
        await _client.BasicConsumeAsync(
            queue: queue,
            autoAck: false,
            consumerTag: "tag",
            noLocal: false,
            exclusive: false,
            arguments: null);

        // Assert
        VerifyLog(LogLevel.Information, $"Starting consumption on queue {queue} for broker ");
        _channelMock.Verify(c => c.BasicConsumeAsync(queue, false, "tag", false, false, null, It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueDeclareAsync_ShouldLogDeclaration()
    {
        // Arrange
        await InitializeClientAsync();
        const string queue = "test-queue";

        // Act
        await _client.QueueDeclareAsync(queue);

        // Assert
        VerifyLog(LogLevel.Information, $"Declaring queue {queue} on broker ");
        _channelMock.Verify(c => c.QueueDeclareAsync(queue, true, false, false, null, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExchangeDeclareAsync_ShouldLogDeclaration()
    {
        // Arrange
        await InitializeClientAsync();
        const string exchange = "test-exchange";

        // Act
        await _client.ExchangeDeclareAsync(exchange);

        // Assert
        VerifyLog(LogLevel.Information, $"Declaring exchange {exchange} on broker ");
        _channelMock.Verify(c => c.ExchangeDeclareAsync(exchange, "topic", true, false, null, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueBindAsync_ShouldLogBinding()
    {
        // Arrange
        await InitializeClientAsync();
        const string queue = "test-queue";
        const string exchange = "test-exchange";
        const string routingKey = "test-key";

        // Act
        await _client.QueueBindAsync(queue, exchange, routingKey);

        // Assert
        VerifyLog(LogLevel.Information, $"Binding queue {queue} to exchange {exchange} with routing key {routingKey} on broker ");
        _channelMock.Verify(c => c.QueueBindAsync(queue, exchange, routingKey, null, false, It.IsAny<CancellationToken>()), Times.Once);
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

    public class TestMessage;
}
