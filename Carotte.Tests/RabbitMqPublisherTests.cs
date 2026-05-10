using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqPublisherTests
{
    public class TestMessage;

    [Fact]
    public async Task PublishAsync_ShouldRespectConvention_WhenExchangeIsNull()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var message = new TestMessage();
        
        // Pass null or string.Empty for exchange to trigger convention
        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            null!);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        var expectedExchange = "message-test";
        
        // Verify exchange is declared as fanout (convention)
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify message is published to this exchange
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            expectedExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var message = new TestMessage();
        
        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            null!);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        // ConnectAsync and ExchangeDeclareAsync should be called only once
        rabbitMqClient.Verify(c => c.ConnectAsync(broker, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldUseExplicitExchange_WhenProvided()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var explicitExchange = "explicit-exchange";
        var message = new TestMessage();
        
        var publisher = new RabbitMqPublisher<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            explicitExchange);

        // Act
        await publisher.PublishAsync(message, CancellationToken.None);

        // Assert
        // Should NOT declare exchange by convention (or at least not the message one in fanout)
        // Currently RabbitMqPublisher doesn't declare anything, it delegates to publication middleware.
        
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            explicitExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
