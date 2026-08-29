using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;

namespace Carotte.Tests;

public class ConsumerMediatorTests
{
    [Fact]
    public void ResolveMessageType_ShouldReturnNullForMultiMessageConsumer_WhenTypeIsMissing()
    {
        // Arrange
        var mediator = CreateMediator<MultiMessageConsumer>();
        var args = CreateDeliveryArgs(type: null);

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBeNull();
    }

    [Fact]
    public void ResolveMessageType_ShouldReturnNullForMultiMessageConsumer_WhenTypeIsUnknown()
    {
        // Arrange
        var mediator = CreateMediator<MultiMessageConsumer>();
        var args = CreateDeliveryArgs("UnknownMessage");

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBeNull();
    }

    [Fact]
    public void ResolveMessageType_ShouldResolveKnownTypeForMultiMessageConsumer()
    {
        // Arrange
        var mediator = CreateMediator<MultiMessageConsumer>();
        var args = CreateDeliveryArgs(nameof(SecondMessage));

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBe(typeof(SecondMessage));
    }

    [Fact]
    public void ResolveMessageType_ShouldResolveKnownTypeByFullNameForMultiMessageConsumer()
    {
        // Arrange
        var mediator = CreateMediator<MultiMessageConsumer>();
        var args = CreateDeliveryArgs(typeof(SecondMessage).FullName);

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBe(typeof(SecondMessage));
    }

    [Fact]
    public void ResolveMessageType_ShouldReturnNullForMultiMessageConsumer_WhenTypeIsEmptyString()
    {
        // Arrange
        var mediator = CreateMediator<MultiMessageConsumer>();
        var args = CreateDeliveryArgs(string.Empty);

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBeNull();
    }

    [Fact]
    public void ResolveMessageType_ShouldInferTypeForSingleMessageConsumer_WhenTypeIsMissing()
    {
        // Arrange
        var mediator = CreateMediator<SingleMessageConsumer>();
        var args = CreateDeliveryArgs(type: null);

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBe(typeof(FirstMessage));
    }

    [Fact]
    public void ResolveMessageType_ShouldReturnNullForSingleMessageConsumer_WhenTypeIsExplicitlySpecifiedButUnknown()
    {
        // Arrange
        var mediator = CreateMediator<SingleMessageConsumer>();
        var args = CreateDeliveryArgs(type: "UnrelatedMessage");

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBeNull();
    }

    [Fact]
    public void ResolveMessageType_ShouldResolveByCustomMessageTypeAttribute()
    {
        // Arrange
        var mediator = CreateMediator<CustomAliasedConsumer>();
        var args = CreateDeliveryArgs(type: "custom.message.type");

        // Act
        var messageType = mediator.ResolveMessageType(args);

        // Assert
        messageType.ShouldBe(typeof(AliasedMessage));
    }

    [Fact]
    public async Task InvokeAsync_ShouldDispatchToSingleMessageConsumer()
    {
        // Arrange
        var services = new ServiceCollection();
        var consumer = new RecordingSingleConsumer();
        services.AddSingleton(consumer);
        var sp = services.BuildServiceProvider();

        var mediator = new ConsumerMediator(sp);
        mediator.Initialize<RecordingSingleConsumer>();

        var message = new FirstMessage();

        // Act
        await mediator.InvokeAsync(sp, typeof(FirstMessage), message, CancellationToken.None);

        // Assert
        consumer.ReceivedMessages.ShouldContain(message);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDispatchToMultiMessageConsumer_ForSpecificMessage()
    {
        // Arrange
        var services = new ServiceCollection();
        var consumer = new RecordingMultiConsumer();
        services.AddSingleton(consumer);
        var sp = services.BuildServiceProvider();

        var mediator = new ConsumerMediator(sp);
        mediator.Initialize<RecordingMultiConsumer>();

        var second = new SecondMessage();

        // Act
        await mediator.InvokeAsync(sp, typeof(SecondMessage), second, CancellationToken.None);

        // Assert
        consumer.ReceivedSecondMessages.ShouldContain(second);
        consumer.ReceivedFirstMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldPropagateExceptionDirectly_WithoutTargetInvocationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<FailingConsumer>();
        var sp = services.BuildServiceProvider();

        var mediator = new ConsumerMediator(sp);
        mediator.Initialize<FailingConsumer>();

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            mediator.InvokeAsync(sp, typeof(FirstMessage), new FirstMessage(), CancellationToken.None));

        ex.Message.ShouldBe("Consumer processing failed");
    }

    [Fact]
    public async Task InvokeAsync_ShouldComplete_WhenMessageTypeIsUnhandled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<RecordingSingleConsumer>();
        var sp = services.BuildServiceProvider();

        var mediator = new ConsumerMediator(sp);
        mediator.Initialize<RecordingSingleConsumer>();

        // Act
        var task = mediator.InvokeAsync(sp, typeof(SecondMessage), new SecondMessage(), CancellationToken.None);

        // Assert
        await task;
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private static ConsumerMediator CreateMediator<TConsumer>()
        where TConsumer : class
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<TConsumer>()
            .BuildServiceProvider();

        var mediator = new ConsumerMediator(serviceProvider);
        mediator.Initialize<TConsumer>();
        return mediator;
    }

    private static BasicDeliverEventArgs CreateDeliveryArgs(string? type)
    {
        var properties = new BasicProperties
        {
            Type = type
        };

        return new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "exchange",
            routingKey: "routing-key",
            properties: properties,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);
    }

    public class FirstMessage;

    public class SecondMessage;

    public class SingleMessageConsumer : IConsumer<FirstMessage>
    {
        public Task HandleAsync(FirstMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MultiMessageConsumer : IConsumer<FirstMessage>, IConsumer<SecondMessage>
    {
        public Task HandleAsync(FirstMessage message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HandleAsync(SecondMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class RecordingSingleConsumer : IConsumer<FirstMessage>
    {
        public List<FirstMessage> ReceivedMessages { get; } = [];

        public Task HandleAsync(FirstMessage message, CancellationToken cancellationToken)
        {
            ReceivedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    public class RecordingMultiConsumer : IConsumer<FirstMessage>, IConsumer<SecondMessage>
    {
        public List<FirstMessage> ReceivedFirstMessages { get; } = [];
        public List<SecondMessage> ReceivedSecondMessages { get; } = [];

        public Task HandleAsync(FirstMessage message, CancellationToken cancellationToken)
        {
            ReceivedFirstMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task HandleAsync(SecondMessage message, CancellationToken cancellationToken)
        {
            ReceivedSecondMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    public class FailingConsumer : IConsumer<FirstMessage>
    {
        public Task HandleAsync(FirstMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Consumer processing failed");
        }
    }

    [MessageType("custom.message.type")]
    public class AliasedMessage;

    public class CustomAliasedConsumer : IConsumer<AliasedMessage>
    {
        public Task HandleAsync(AliasedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
