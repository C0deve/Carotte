using Moq;
using RabbitMQ.Client;
using Shouldly;

namespace Carotte.Tests;

public class RoutingKeyTests
{
    [Fact]
    public async Task SendAsync_ShouldUseClassNameAsDefaultRoutingKey()
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

        var producer = new RabbitMqProducer<TestMessage>(
            connectionManagerMock.Object, 
            serializerMock.Object, 
            "test-broker", 
            "test-exchange");

        // Act
        await producer.SendAsync(message);

        // Assert
        // On vérifie que BasicPublishAsync a été appelé avec le nom de la classe comme routingKey
        channelMock.Verify(c => c.BasicPublishAsync(
            It.Is<string>(e => e == "test-exchange"),
            It.Is<string>(r => r == nameof(TestMessage)),
            It.IsAny<bool>(),
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
