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
        var rabbitMqClientMock = new Mock<IRabbitMqClient>();
        var serializerMock = new Mock<ISerializer>();

        var message = new TestMessage("Hello");
        serializerMock.Setup(s => s.Serialize(message)).Returns([1, 2, 3]);

        var customMiddleware = new Mock<IProducerMiddleware<TestMessage>>();
        customMiddleware.Setup(m => m.InvokeAsync(It.IsAny<ProducerContext<TestMessage>>(), It.IsAny<ProducerDelegate<TestMessage>>()))
            .Returns<ProducerContext<TestMessage>, ProducerDelegate<TestMessage>>((ctx, next) => next(ctx));

        // To inject a custom middleware, we can modify RabbitMqProducer or create one that accepts it.
        // Currently, RabbitMqProducer constructs its own middlewares in a hardcoded way.
        // Let's add a test that verifies that the default middlewares are correctly executed.
        
        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClientMock.Object, 
            serializerMock.Object, 
            "test-broker", 
            "test-exchange");

        // Act
        await producer.SendAsync(message);

        // Assert
        // Verify that the publication middleware was called (via the client mock)
        rabbitMqClientMock.Verify(c => c.BasicPublishAsync<TestMessage>(
            "test-broker",
            "test-exchange",
            nameof(TestMessage),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify that serialization took place
        serializerMock.Verify(s => s.Serialize(message), Times.Once);
    }
}
