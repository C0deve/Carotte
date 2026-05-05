using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqProducerTests
{
    public class TestMessage;

    [Fact]
    public async Task SendAsync_ShouldRespectConvention_WhenExchangeIsNull()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var message = new TestMessage();
        
        // On passe null ou string.Empty pour l'exchange pour déclencher la convention
        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            null!);

        // Act
        await producer.SendAsync(message, CancellationToken.None);

        // Assert
        var expectedExchange = typeof(TestMessage).FullName;
        
        // Vérifier que l'exchange est déclaré en fanout (convention)
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        // Vérifier que le message est publié sur cet exchange
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            expectedExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var message = new TestMessage();
        
        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            null!);

        // Act
        await producer.SendAsync(message, CancellationToken.None);
        await producer.SendAsync(message, CancellationToken.None);

        // Assert
        // ConnectAsync et ExchangeDeclareAsync ne doivent être appelés qu'une seule fois
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
    public async Task SendAsync_ShouldUseExplicitExchange_WhenProvided()
    {
        // Arrange
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        var explicitExchange = "explicit-exchange";
        var message = new TestMessage();
        
        var producer = new RabbitMqProducer<TestMessage>(
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            explicitExchange);

        // Act
        await producer.SendAsync(message, CancellationToken.None);

        // Assert
        // Ne devrait PAS déclarer l'exchange par convention (ou du moins pas celui du message en fanout)
        // Actuellement RabbitMqProducer ne déclare rien, il délègue au middleware de publication.
        
        rabbitMqClient.Verify(c => c.BasicPublishAsync<TestMessage>(
            explicitExchange,
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<BasicProperties>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
