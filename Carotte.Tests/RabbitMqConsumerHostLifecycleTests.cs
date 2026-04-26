using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace Carotte.Tests;

public class RabbitMqConsumerHostLifecycleTests
{
    private readonly Mock<IConnectionManager> _connectionManagerMock = new();
    private readonly Mock<ISerializer> _serializerMock = new();
    private readonly Mock<IConnection> _connectionMock = new();
    private readonly Mock<IChannel> _channelMock = new();

    public RabbitMqConsumerHostLifecycleTests()
    {
        _connectionManagerMock.Setup(m => m.GetConnectionAsync(It.IsAny<string>())).ReturnsAsync(_connectionMock.Object);
        _connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(_channelMock.Object);
        _channelMock.Setup(c => c.IsOpen).Returns(true);
    }

    public record TestMessage(string Content);
    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task EachHost_ShouldRegisterAndUnregisterInConnectionManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var brokerName = "test-broker";
        var queueAttrs = new List<QueueAttribute> { new QueueAttribute("test-queue", brokerName) };

        services.AddSingleton(_connectionManagerMock.Object);
        services.AddSingleton(_serializerMock.Object);
        services.AddSingleton(Mock.Of<ILogger<RabbitMqConsumerHost<TestConsumer>>>());
        services.AddSingleton(Mock.Of<ILogger<RabbitMqClient>>());
        services.AddTransient<ConsumerMediator>();
        services.AddSingleton(typeof(TestConsumer));
        services.AddTransient<IRabbitMqClient, RabbitMqClient>();
        services.AddLogging();

        var sp = services.BuildServiceProvider();
        var host1 = ActivatorUtilities.CreateInstance<RabbitMqConsumerHost<TestConsumer>>(sp, brokerName, queueAttrs);
        var host2 = ActivatorUtilities.CreateInstance<RabbitMqConsumerHost<TestConsumer>>(sp, brokerName, queueAttrs);

        // Act
        var cts = new CancellationTokenSource();
        await host1.StartAsync(cts.Token);
        await host2.StartAsync(cts.Token);

        // Assert
        _connectionManagerMock.Verify(m => m.RegisterHostAsync(brokerName), Times.Exactly(2));
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        
        await host1.StopAsync(cts.Token);
        await host2.StopAsync(cts.Token);

        _connectionManagerMock.Verify(m => m.UnregisterHostAsync(brokerName), Times.Exactly(2));
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var brokerName = "test-broker";
        var queueAttrs = new List<QueueAttribute> { new QueueAttribute("test-queue", brokerName) };

        services.AddSingleton(_connectionManagerMock.Object);
        services.AddSingleton(_serializerMock.Object);
        services.AddSingleton(Mock.Of<ILogger<RabbitMqConsumerHost<TestConsumer>>>());
        services.AddSingleton(Mock.Of<ILogger<RabbitMqClient>>());
        services.AddTransient<ConsumerMediator>();
        services.AddSingleton(typeof(TestConsumer));
        services.AddTransient<IRabbitMqClient, RabbitMqClient>();
        services.AddLogging();

        var sp = services.BuildServiceProvider();
        var host = ActivatorUtilities.CreateInstance<RabbitMqConsumerHost<TestConsumer>>(sp, brokerName, queueAttrs);

        var cts = new CancellationTokenSource();
        await host.StartAsync(cts.Token);
        
        // Act
        await host.StopAsync(cts.Token);

        // Assert
        _connectionManagerMock.Verify(m => m.UnregisterHostAsync(brokerName), Times.Once);
    }
}
