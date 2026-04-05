using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqConsumerHostTests
{
    [Fact]
    public async Task StartAsync_ShouldInitializeAndSetupTopology()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var connectionManager = new Mock<IConnectionManager>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        List<QueueAttribute> queueAttributes =
        [
            new("test-queue", "test-broker", "test-exchange", "test-key")
        ];

        var connection = new Mock<IConnection>();
        var channel = new Mock<IChannel>();

        connectionManager.Setup(m => m.GetConnectionAsync(broker))
            .ReturnsAsync(connection.Object);
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        channel.Setup(c => c.IsOpen).Returns(true);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            connectionManager.Object,
            serializer.Object,
            broker,
            queueAttributes);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        connectionManager.Verify(m => m.GetConnectionAsync(broker), Times.Once);
        connection.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Check topology setup
        channel.Verify(c => c.QueueDeclareAsync("test-queue", true, false, false, It.IsAny<IDictionary<string, object>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.ExchangeDeclareAsync("test-exchange", "topic", true, false, It.IsAny<IDictionary<string, object>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.QueueBindAsync("test-queue", "test-exchange", "test-key", It.IsAny<IDictionary<string, object>>(), false, It.IsAny<CancellationToken>()), Times.Once);
        
        // Check consumer setup
        channel.Verify(c => c.BasicConsumeAsync("test-queue", false, string.Empty, false, false, It.IsAny<IDictionary<string, object>?>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
        // We cannot easily verify CloseAsync via Moq because it is an extension, 
        // but CloseChannelAsync calls Dispose() at the end.
        channel.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_ShouldDisposeResources()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var connectionManager = new Mock<IConnectionManager>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        List<QueueAttribute> queueAttributes = [];
        
        var connection = new Mock<IConnection>();
        var channel = new Mock<IChannel>();

        connectionManager.Setup(m => m.GetConnectionAsync(broker))
            .ReturnsAsync(connection.Object);
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            connectionManager.Object,
            serializer.Object,
            broker,
            queueAttributes);

        // Initialize the channel
        await host.StartAsync(CancellationToken.None);

        // Act
        host.Dispose();

        // Assert
        channel.Verify(c => c.Dispose(), Times.Once);
    }

    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
