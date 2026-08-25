using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RoutingKeyTests
{
    [Fact]
    public async Task PublishAsync_ShouldUseClassNameAsDefaultRoutingKey()
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

        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClientMock.Object,
            serializerMock.Object,
            Mock.Of<ILogger<RabbitMqPublisher<TestMessage>>>(),
            broker,
            exchange);

        // Act
        await publisher.PublishAsync(message);

        // Assert
        // Verify that BasicPublishAsync was called with the class name as routingKey
        rabbitMqClientMock.Verify(c => c.BasicPublishAsync<TestMessage>(
            exchange,
            nameof(TestMessage),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public record TestMessage(string Content);
}
