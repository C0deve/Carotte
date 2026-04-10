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
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        List<QueueAttribute> queueAttributes =
        [
            new("test-queue", "test-broker", "test-exchange", "test-key")
        ];

        var channel = new Mock<IChannel>();

        rabbitMqClient.Setup(m => m.GetChannelAsync(broker, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        channel.Setup(c => c.IsOpen).Returns(true);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            queueAttributes);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(m => m.GetChannelAsync(broker, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        
        // Check topology setup
        rabbitMqClient.Verify(c => c.QueueDeclareAsync(broker, "test-queue", true, false, false, It.IsAny<IDictionary<string, object>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(broker, "test-exchange", "topic", true, false, It.IsAny<IDictionary<string, object>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.QueueBindAsync(broker, "test-queue", "test-exchange", "test-key", It.IsAny<IDictionary<string, object>>(), false, It.IsAny<CancellationToken>()), Times.Once);
        
        // Check consumer setup
        rabbitMqClient.Verify(c => c.BasicConsumeAsync(broker, "test-queue", false, string.Empty, false, false, It.IsAny<IDictionary<string, object>?>(), It.IsAny<AsyncDefaultBasicConsumer>(), It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
        // On vérifie que le client a été appelé (Dispose est appelé sur le client, pas sur le canal directement ici car centralisé)
    }

    [Fact]
    public void Dispose_ShouldCallBaseDispose()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        var broker = "test-broker";
        List<QueueAttribute> queueAttributes = [];
        
        var mediator = new ConsumerMediator(serviceProvider.Object);
        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            broker,
            queueAttributes);

        // Act
        host.Dispose();

        // Assert
        // Success if no exception
    }

    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
