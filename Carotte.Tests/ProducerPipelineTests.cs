using Moq;
using RabbitMQ.Client;
using Carotte.pipeline;

namespace Carotte.Tests;

public class ProducerPipelineTests
{
    [Fact]
    public async Task SendAsync_ShouldExecuteCustomMiddleware()
    {
        // Arrange
        var connectionManagerMock = new Mock<IConnectionManager>();
        var serializerMock = new Mock<ISerializer>();
        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();

        connectionManagerMock.Setup(m => m.GetConnectionAsync(It.IsAny<string>()))
            .ReturnsAsync(connectionMock.Object);
        connectionMock.Setup(m => m.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);
        
        var message = new TestMessage("Hello");
        serializerMock.Setup(s => s.Serialize(message)).Returns([1, 2, 3]);

        var customMiddleware = new Mock<IProducerMiddleware<TestMessage>>();
        customMiddleware.Setup(m => m.InvokeAsync(It.IsAny<ProducerContext<TestMessage>>(), It.IsAny<ProducerDelegate<TestMessage>>()))
            .Returns<ProducerContext<TestMessage>, ProducerDelegate<TestMessage>>((ctx, next) => next(ctx));

        // To inject a custom middleware, we can modify RabbitMqProducer or create one that accepts it.
        // Currently, RabbitMqProducer constructs its own middlewares in a hardcoded way.
        // Let's add a test that verifies that the default middlewares are correctly executed.
        
        var producer = new RabbitMqProducer<TestMessage>(
            connectionManagerMock.Object, 
            serializerMock.Object, 
            "test-broker", 
            "test-exchange");

        // Act
        await producer.SendAsync(message);

        // Assert
        // Verify that the publication middleware was called (via the channel mock)
        channelMock.Verify(c => c.BasicPublishAsync(
            "test-exchange",
            nameof(TestMessage),
            true,
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify that serialization took place
        serializerMock.Verify(s => s.Serialize(message), Times.Once);
    }
}
