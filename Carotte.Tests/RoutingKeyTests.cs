using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RoutingKeyTests
{
    [Fact]
    public async Task SendAsync_ShouldUseClassNameAsDefaultRoutingKey()
    {
        // Arrange
        var rabbitMqClientMock = new Mock<IRabbitMqClient>();
        var serializerMock = new Mock<ISerializer>();

        var message = new TestMessage("Hello");
        serializerMock.Setup(s => s.Serialize(message)).Returns([1, 2, 3]);

        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClientMock.Object, 
            serializerMock.Object, 
            "test-broker", 
            "test-exchange");

        // Act
        await producer.SendAsync(message);

        // Assert
        // On vérifie que BasicPublishAsync a été appelé avec le nom de la classe comme routingKey
        rabbitMqClientMock.Verify(c => c.BasicPublishAsync<TestMessage>(
            It.Is<string>(b => b == "test-broker"),
            It.Is<string>(e => e == "test-exchange"),
            It.Is<string>(r => r == nameof(TestMessage)),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
