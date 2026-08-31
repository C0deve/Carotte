using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;

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
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [new BindingInfo("test-exchange", "test-key")],
            Arguments: ReadOnlyDictionary<string, object>.Empty);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        VerifyLog(loggerMock,
            LogLevel.Information,
            "Starting RabbitMqConsumerHost for TestConsumer on broker test-broker. Queue: test-queue, Exchanges: test-exchange, Messages: 'TestMessage'");
        VerifyLog(loggerMock, LogLevel.Information, "Setting up topology for TestConsumer");

        rabbitMqClient.Verify(c => c.ConnectAsync(broker, It.IsAny<CancellationToken>()), Times.Once);

        // Check topology setup
        rabbitMqClient.Verify(c => c.QueueDeclareAsync("test-queue",
                true,
                false,
                false,
                It.IsAny<IDictionary<string, object?>>(),
                false,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        rabbitMqClient.Verify(c => c.QueueBindAsync("test-queue",
                "test-exchange",
                "test-key",
                It.IsAny<IDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Check consumer setup
        rabbitMqClient.Verify(c => c.BasicConsumeAsync("test-queue",
                false,
                string.Empty,
                false,
                false,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        await host.StopAsync(CancellationToken.None);
        rabbitMqClient.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupDefaultQos()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [], Arguments: null,
            PrefetchCount: 1); // Default is now 1

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicQosAsync(0, 1, false, It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupQos()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: "test-queue",
            Bindings: [], Arguments: null,
            PrefetchCount: 15);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.BasicQosAsync(0, 15, false, It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldSetupMultipleQueueAttributes()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        const string testQueue = "test-queue";
        const string testExchange = "test-exchange";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: testQueue,
            Bindings: [
                new BindingInfo(testExchange, "key1"),
                new BindingInfo(testExchange, "key2")
            ], Arguments: null);

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        VerifyLog(loggerMock, LogLevel.Information, "Starting RabbitMqConsumerHost for TestConsumer on broker test-broker. Queue: test-queue, Exchanges: test-exchange, Messages: 'TestMessage'");
        VerifyLog(loggerMock, LogLevel.Information, "Setting up topology for TestConsumer");

        rabbitMqClient.Verify(c => c.QueueDeclareAsync(testQueue,
                true,
                false,
                false,
                It.IsAny<IDictionary<string, object?>>(),
                false,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

        rabbitMqClient.Verify(c => c.QueueBindAsync(testQueue,
                testExchange,
                "key1",
                It.IsAny<IDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        rabbitMqClient.Verify(c => c.QueueBindAsync(testQueue,
                testExchange,
                "key2",
                It.IsAny<IDictionary<string, object?>>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should consume only once from "test-queue"
        rabbitMqClient.Verify(c => c.BasicConsumeAsync(testQueue,
                false,
                string.Empty,
                false,
                false,
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldDeclareDeadLetterExchangeAndQueueArguments_WhenDeadLetterExchangeIsConfigured()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        const string queue = "test-queue";
        const string deadLetterExchange = "test-dlx";
        const string deadLetterRoutingKey = "failed";
        IConsumerTopology topology = new ConsumerAttributeTopology(
            Broker: broker,
            Queue: queue,
            Bindings: [], Arguments: null,
            ErrorStrategy: new ConsumerErrorStrategy(
                DeadLetterExchange: deadLetterExchange,
                DeadLetterRoutingKey: deadLetterRoutingKey));

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            topology);

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            deadLetterExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.QueueDeclareAsync(
            queue,
            true,
            false,
            false,
            It.Is<IDictionary<string, object?>>(args =>
                (string)args["x-dead-letter-exchange"]! == deadLetterExchange &&
                (string)args["x-dead-letter-routing-key"]! == deadLetterRoutingKey),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldApplyDefaultErrorStrategyConvention_WhenNoStrategyIsProvided()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        const string queue = "test-queue";
        const string expectedDeadLetterExchange = "x.dlx.test-queue";
        const string expectedDeadLetterQueue = "q.dlq.test-queue";

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>().Object,
            broker,
            new ConsumerAttributeTopology(Broker: broker, Queue: queue, Bindings: [], Arguments: null));

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedDeadLetterExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.QueueDeclareAsync(
            expectedDeadLetterQueue,
            true,
            false,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.QueueBindAsync(
            expectedDeadLetterQueue,
            expectedDeadLetterExchange,
            queue,
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        rabbitMqClient.Verify(c => c.QueueDeclareAsync(
            queue,
            true,
            false,
            false,
            It.Is<IDictionary<string, object?>>(args =>
                (string)args["x-dead-letter-exchange"]! == expectedDeadLetterExchange &&
                (string)args["x-dead-letter-routing-key"]! == queue),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldCloseAndDisposeRabbitMqClient()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";

        var mediator = new ConsumerMediator(serviceProvider.Object);
        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>();

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            new ConsumerAttributeTopology(Broker: broker, Queue: "test-queue", Bindings: [], Arguments: null));

        // Act
        await host.StopAsync(CancellationToken.None);

        // Assert
        rabbitMqClient.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldNackWithoutRequeue_WhenMessageTypeCannotBeResolved()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var serializer = new Mock<ISerializer>();
        const string broker = "test-broker";
        const ulong deliveryTag = 42;

        var mediator = new ConsumerMediator(serviceProvider.Object);
        mediator.Initialize<MultiMessageConsumer>();

        var loggerMock = new Mock<ILogger<RabbitMqConsumerHost<MultiMessageConsumer>>>();
        var host = new RabbitMqConsumerHost<MultiMessageConsumer>(
            mediator,
            rabbitMqClient.Object,
            serializer.Object,
            loggerMock.Object,
            broker,
            new ConsumerAttributeTopology(Broker: broker, Queue: "test-queue", Bindings: [], Arguments: null));

        var deliveryArgs = CreateDeliveryArgs(deliveryTag, type: null);

        // Act
        await InvokeHandleMessageAsync(host, deliveryArgs);

        // Assert
        rabbitMqClient.Verify(c => c.BasicNackAsync(deliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldRetryAndAck_WhenProcessingEventuallySucceeds()
    {
        // Arrange
        var consumer = new RetryConsumer(failuresBeforeSuccess: 2);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(consumer)
            .BuildServiceProvider();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        const string broker = "test-broker";
        const ulong deliveryTag = 43;

        var mediator = new ConsumerMediator(serviceProvider);
        mediator.Initialize<RetryConsumer>();

        var host = new RabbitMqConsumerHost<RetryConsumer>(
            mediator,
            rabbitMqClient.Object,
            new ActivatorSerializer(),
            new Mock<ILogger<RabbitMqConsumerHost<RetryConsumer>>>().Object,
            broker,
            new ConsumerAttributeTopology(
                Broker: broker,
                Queue: "test-queue",
                Bindings: [], Arguments: null,
                ErrorStrategy: new ConsumerErrorStrategy(MaxRetryAttempts: 2)));

        InvokeBuildPipeline(host);

        // Act
        await InvokeHandleMessageAsync(host, CreateDeliveryArgs(deliveryTag, nameof(TestMessage)));

        // Assert
        consumer.Attempts.ShouldBe(3);
        rabbitMqClient.Verify(c => c.BasicAckAsync(deliveryTag, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldRetryThenNackWithConfiguredRequeue_WhenProcessingKeepsFailing()
    {
        // Arrange
        var consumer = new RetryConsumer(failuresBeforeSuccess: int.MaxValue);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(consumer)
            .BuildServiceProvider();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        const string broker = "test-broker";
        const ulong deliveryTag = 44;

        var mediator = new ConsumerMediator(serviceProvider);
        mediator.Initialize<RetryConsumer>();

        var host = new RabbitMqConsumerHost<RetryConsumer>(
            mediator,
            rabbitMqClient.Object,
            new ActivatorSerializer(),
            new Mock<ILogger<RabbitMqConsumerHost<RetryConsumer>>>().Object,
            broker,
            new ConsumerAttributeTopology(
                Broker: broker,
                Queue: "test-queue",
                Bindings: [], Arguments: null,
                ErrorStrategy: new ConsumerErrorStrategy(
                    MaxRetryAttempts: 1,
                    FailureAction: ConsumerFailureAction.Requeue)));

        InvokeBuildPipeline(host);

        // Act
        await InvokeHandleMessageAsync(host, CreateDeliveryArgs(deliveryTag, nameof(TestMessage)));

        // Assert
        consumer.Attempts.ShouldBe(2);
        rabbitMqClient.Verify(c => c.BasicNackAsync(deliveryTag, false, true, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldNotRetry_WhenExceptionIsJsonException()
    {
        // Arrange
        var consumer = new TestConsumer();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(consumer)
            .BuildServiceProvider();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        var failingSerializer = new Mock<ISerializer>();
        failingSerializer
            .Setup(s => s.Deserialize<TestMessage>(It.IsAny<byte[]>()))
            .Throws(new System.Text.Json.JsonException("Malformed JSON"));

        const string broker = "test-broker";
        const ulong deliveryTag = 46;

        var mediator = new ConsumerMediator(serviceProvider);
        mediator.Initialize<TestConsumer>();

        var host = new RabbitMqConsumerHost<TestConsumer>(
            mediator,
            rabbitMqClient.Object,
            failingSerializer.Object,
            new Mock<ILogger<RabbitMqConsumerHost<TestConsumer>>>().Object,
            broker,
            new ConsumerAttributeTopology(
                Broker: broker,
                Queue: "test-queue",
                Bindings: [], Arguments: null,
                ErrorStrategy: new ConsumerErrorStrategy(MaxRetryAttempts: 3)));

        InvokeBuildPipeline(host);

        // Act
        await InvokeHandleMessageAsync(host, CreateDeliveryArgs(deliveryTag, nameof(TestMessage)));

        // Assert
        failingSerializer.Verify(s => s.Deserialize<TestMessage>(It.IsAny<byte[]>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicNackAsync(deliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_ShouldNotRetry_WhenMaxRetryAttemptsIsExplicitlyZero()
    {
        // Arrange
        var consumer = new RetryConsumer(failuresBeforeSuccess: int.MaxValue);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(consumer)
            .BuildServiceProvider();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        const string broker = "test-broker";
        const ulong deliveryTag = 45;

        var mediator = new ConsumerMediator(serviceProvider);
        mediator.Initialize<RetryConsumer>();

        var host = new RabbitMqConsumerHost<RetryConsumer>(
            mediator,
            rabbitMqClient.Object,
            new ActivatorSerializer(),
            new Mock<ILogger<RabbitMqConsumerHost<RetryConsumer>>>().Object,
            broker,
            new ConsumerAttributeTopology(
                Broker: broker,
                Queue: "test-queue",
                Bindings: [], Arguments: null,
                ErrorStrategy: new ConsumerErrorStrategy(MaxRetryAttempts: 0)));

        InvokeBuildPipeline(host);

        // Act
        await InvokeHandleMessageAsync(host, CreateDeliveryArgs(deliveryTag, nameof(TestMessage)));

        // Assert
        consumer.Attempts.ShouldBe(1);
        rabbitMqClient.Verify(c => c.BasicNackAsync(deliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);
        rabbitMqClient.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Queue("test-queue")]
    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MultiMessageConsumer : IConsumer<TestMessage>, IConsumer<OtherTestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HandleAsync(OtherTestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class RetryConsumer(int failuresBeforeSuccess) : IConsumer<TestMessage>
    {
        public int Attempts { get; private set; }

        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts <= failuresBeforeSuccess
                ? throw new InvalidOperationException("Simulated processing failure.")
                : Task.CompletedTask;
        }
    }

    private sealed class ActivatorSerializer : ISerializer
    {
        public byte[] Serialize<T>(T message) => [];

        public T Deserialize<T>(byte[] data) => Activator.CreateInstance<T>();
    }

    private static void VerifyLog<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string message)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == message),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public class TestMessage;

    public class OtherTestMessage;

    private static BasicDeliverEventArgs CreateDeliveryArgs(ulong deliveryTag, string? type)
    {
        var properties = new BasicProperties
        {
            Type = type
        };

        return new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: deliveryTag,
            redelivered: false,
            exchange: "exchange",
            routingKey: "routing-key",
            properties: properties,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);
    }

    private static async Task InvokeHandleMessageAsync<TConsumer>(
        RabbitMqConsumerHost<TConsumer> host,
        BasicDeliverEventArgs deliveryArgs)
        where TConsumer : class
    {
        var method = typeof(RabbitMqConsumerHost<TConsumer>).GetMethod(
            "HandleMessageAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = (Task?)method.Invoke(host, [deliveryArgs, CancellationToken.None]);
        Assert.NotNull(task);
        await task;
    }

    private static void InvokeBuildPipeline<TConsumer>(RabbitMqConsumerHost<TConsumer> host)
        where TConsumer : class
    {
        var method = typeof(RabbitMqConsumerHost<TConsumer>).GetMethod(
            "BuildPipeline",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(host, []);
    }
}
