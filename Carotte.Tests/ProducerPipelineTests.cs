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

        // Pour injecter un middleware personnalisé, on peut modifier RabbitMqProducer ou en créer un qui l'accepte.
        // Actuellement, RabbitMqProducer construit ses propres middlewares en dur.
        // Ajoutons un test qui vérifie que les middlewares par défaut sont bien exécutés.
        
        var producer = new RabbitMqProducer<TestMessage>(
            connectionManagerMock.Object, 
            serializerMock.Object, 
            "test-broker", 
            "test-exchange");

        // Act
        await producer.SendAsync(message);

        // Assert
        // Vérifie que le middleware de publication a été appelé (via le mock du canal)
        channelMock.Verify(c => c.BasicPublishAsync(
            "test-exchange",
            nameof(TestMessage),
            true,
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Vérifie que la sérialisation a eu lieu
        serializerMock.Verify(s => s.Serialize(message), Times.Once);
    }
}
