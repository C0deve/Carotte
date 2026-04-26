using Moq;
using RabbitMQ.Client;
using Carotte.pipeline;

namespace Carotte.Tests;

public class ProducerPipelineTests
{
    [Fact]
    public async Task SendAsync_ShouldExecutePipelineWithChannel()
    {
        // Arrange
        var rabbitMqClientMock = new Mock<IRabbitMqClient>();
        var connectionManagerMock = new Mock<IConnectionManager>();
        var serializerMock = new Mock<ISerializer>();
        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();

        var broker = "test-broker";
        var exchange = "test-exchange";
        var message = new TestMessage("Hello");

        connectionManagerMock.Setup(m => m.GetConnectionAsync(broker)).ReturnsAsync(connectionMock.Object);
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(channelMock.Object);
        channelMock.Setup(c => c.IsOpen).Returns(true);
        serializerMock.Setup(s => s.Serialize(message)).Returns([1, 2, 3]);

        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClientMock.Object,
            serializerMock.Object,
            broker,
            exchange);

        // Act
        await producer.SendAsync(message);

        // Assert
        // Verify that the publication middleware was called (via the client mock)
        rabbitMqClientMock.Verify(c => c.BasicPublishAsync<TestMessage>(
            exchange,
            nameof(TestMessage),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify that serialization took place
        serializerMock.Verify(s => s.Serialize(message), Times.Once);
        
        await producer.DisposeAsync();
        rabbitMqClientMock.Verify(c => c.DisposeAsync(), Times.Once);
    }

    public record TestMessage(string Content);
}
