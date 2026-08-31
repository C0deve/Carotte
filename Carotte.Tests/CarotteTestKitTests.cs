using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Carotte.Tests;

public class CarotteTestKitTests
{
    public record TestMessage(string Content);

    [Published]
    public record ResponseMessage(string Content);

    public record UnregisteredMessage(string Info);

    [Queue("test-queue", broker: "test-broker")]
    public class TestConsumer(IPublisher<ResponseMessage> publisher) : IConsumer<TestMessage>
    {
        public async Task HandleAsync(TestMessage message, CancellationToken cancellationToken = default)
        {
            await publisher.PublishAsync(new ResponseMessage($"Received: {message.Content}"), cancellationToken);
        }
    }

    public class NoAttributeConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class ScopeTracker
    {
        public List<Guid> ConsumerScopeIds { get; } = [];
        public int DisposedScopes { get; set; }
    }

    public sealed class ScopedDependency(ScopeTracker tracker) : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public void Dispose() => tracker.DisposedScopes++;
    }

    [Queue("scoped-consumer-queue", broker: "test-broker")]
    public class ScopedConsumer(ScopedDependency dependency, ScopeTracker tracker) : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            tracker.ConsumerScopeIds.Add(dependency.Id);
            return Task.CompletedTask;
        }
    }

    [Queue("retry-consumer-queue", broker: "test-broker", maxRetryAttempts: 2)]
    public class RetryConsumer : IConsumer<TestMessage>
    {
        public static int Attempts { get; set; }

        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts < 2)
            {
                throw new InvalidOperationException("Transient failure");
            }

            return Task.CompletedTask;
        }
    }

    [Queue("failing-consumer-queue", broker: "test-broker", maxRetryAttempts: 2)]
    public class AlwaysFailingConsumer : IConsumer<TestMessage>
    {
        public static int Attempts { get; set; }

        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new InvalidOperationException("Permanent failure");
        }
    }

    [Queue("requeue-failing-consumer-queue", broker: "test-broker", maxRetryAttempts: 1, failureAction: ConsumerFailureAction.Requeue)]
    public class RequeueFailingConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Permanent failure with requeue");
        }
    }

    [Queue("arbitrary-publisher-queue", broker: "test-broker")]
    public class ArbitraryPublisherConsumer(IPublisher<UnregisteredMessage> publisher) : IConsumer<TestMessage>
    {
        public async Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            await publisher.PublishAsync(new UnregisteredMessage(message.Content), cancellationToken);
        }
    }

    public record AutoDispatchMessage(string Content);

    [Queue("auto-dispatch-queue", broker: "test-broker")]
    public class AutoDispatchConsumer(IPublisher<ResponseMessage> publisher) : IConsumer<AutoDispatchMessage>
    {
        public async Task HandleAsync(AutoDispatchMessage message, CancellationToken cancellationToken)
        {
            await publisher.PublishAsync(new ResponseMessage($"Auto: {message.Content}"), cancellationToken);
        }
    }

    public record BroadcastMessage(string Text);

    [Queue("broadcast-one-queue", broker: "test-broker")]
    public class FirstBroadcastConsumer(IPublisher<ResponseMessage> publisher) : IConsumer<BroadcastMessage>
    {
        public async Task HandleAsync(BroadcastMessage message, CancellationToken cancellationToken)
        {
            await publisher.PublishAsync(new ResponseMessage($"First: {message.Text}"), cancellationToken);
        }
    }

    [Queue("broadcast-two-queue", broker: "test-broker")]
    public class SecondBroadcastConsumer(IPublisher<ResponseMessage> publisher) : IConsumer<BroadcastMessage>
    {
        public async Task HandleAsync(BroadcastMessage message, CancellationToken cancellationToken)
        {
            await publisher.PublishAsync(new ResponseMessage($"Second: {message.Text}"), cancellationToken);
        }
    }

    public class IncompatibleConsumer : IConsumer<ResponseMessage>
    {
        public Task HandleAsync(ResponseMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task SimulateReceive_ShouldInvokeConsumer_AndStoreSentMessages()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddCarotte(c => c
                .AddBroker("test-broker", _ => { })
                .ScanAssemblies(typeof(TestConsumer).Assembly))
            .AddCarotteTestKit().BuildServiceProvider();
        var testKit = sp.GetRequiredService<CarotteTestKit>();
        var testMessage = new TestMessage("Hello Carotte");

        // Act
        await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(testMessage);

        // Assert
        var sentMessages = testKit.GetSentMessages<ResponseMessage>();
        sentMessages.Count.ShouldBe(1);
        sentMessages[0].Content.ShouldBe("Received: Hello Carotte");
    }

    [Fact]
    public async Task Publisher_ShouldBeMockable_WithMoq()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddCarotte(c => c
                .AddBroker("test-broker", _ => { })
                .ScanAssemblies(typeof(TestConsumer).Assembly))
            .AddCarotteTestKit()
            // Enregistrement explicite du mock
            .AddMockPublisher<ResponseMessage>()
            .BuildServiceProvider();

        var testKit = sp.GetRequiredService<CarotteTestKit>();
        var mockPublisher = sp.GetMockPublisher<ResponseMessage>();

        var testMessage = new TestMessage("Mock Me");

        // Act
        await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(testMessage);

        // Assert
        mockPublisher.Verify(p => p.PublishAsync(It.Is<ResponseMessage>(r => r.Content == "Received: Mock Me"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SimulateReceive_ShouldCreateAndDisposeOneScopePerMessage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ScopeTracker>();
        services.AddScoped<ScopedDependency>();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(ScopedConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<ScopedConsumer, TestMessage>(new TestMessage("first"));
        await testKit.SimulateReceiveAsync<ScopedConsumer, TestMessage>(new TestMessage("second"));

        var tracker = serviceProvider.GetRequiredService<ScopeTracker>();
        tracker.ConsumerScopeIds.Count.ShouldBe(2);
        tracker.ConsumerScopeIds.Distinct().Count().ShouldBe(2);
        tracker.DisposedScopes.ShouldBe(2);
    }

    [Fact]
    public async Task OpenGenericPublisher_ShouldWorkForAnyUnregisteredMessageType()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(ArbitraryPublisherConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<ArbitraryPublisherConsumer, TestMessage>(new TestMessage("custom payload"));

        var messages = testKit.GetSentMessages<UnregisteredMessage>();
        messages.Count.ShouldBe(1);
        messages[0].Info.ShouldBe("custom payload");
    }

    [Fact]
    public async Task SimulateReceive_ShouldRetryAndSucceed_WhenTransientFailureOccurs()
    {
        RetryConsumer.Attempts = 0;
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(RetryConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<RetryConsumer, TestMessage>(new TestMessage("retry"));

        RetryConsumer.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task SimulateReceive_ShouldReturnNackResult_WhenMaxRetryAttemptsExceeded()
    {
        AlwaysFailingConsumer.Attempts = 0;
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(AlwaysFailingConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<AlwaysFailingConsumer, TestMessage>(new TestMessage("fail"));

        result.IsNacked.ShouldBeTrue();
    }

    [Fact]
    public async Task SimulateReceive_ShouldReturnAckResult_WhenMessageProcessedSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(new TestMessage("hello"));

        result.IsAcked.ShouldBeTrue();
    }

    [Fact]
    public async Task SimulateReceive_ShouldMeasureElapsedTime_WhenMessageProcessed()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(new TestMessage("hello"));

        result.ElapsedTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SimulateReceive_ShouldContainException_WhenConsumerFails()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(AlwaysFailingConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<AlwaysFailingConsumer, TestMessage>(new TestMessage("fail"));

        result.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SimulateReceive_ShouldSetRequeuedFalse_WhenDefaultFailureActionIsDeadLetter()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(AlwaysFailingConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<AlwaysFailingConsumer, TestMessage>(new TestMessage("fail"));

        result.Requeued.ShouldBeFalse();
    }

    [Fact]
    public async Task SimulateReceive_ShouldSetRequeuedTrue_WhenFailureActionIsRequeue()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(RequeueFailingConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var result = await testKit.SimulateReceiveAsync<RequeueFailingConsumer, TestMessage>(new TestMessage("fail"));

        result.Requeued.ShouldBeTrue();
    }

    [Fact]
    public async Task SimulateReceive_NonGeneric_ShouldReturnListOfResults_ForBroadcastMessage()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(FirstBroadcastConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var results = await testKit.SimulateReceiveAsync(new BroadcastMessage("hello"));

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SimulateReceive_ShouldExecuteTracingMiddleware()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CarotteDiagnostics.ServiceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = act => activities.Add(act)
        };
        ActivitySource.AddActivityListener(listener);

        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(new TestMessage("tracing test"));

        var activity = activities.FirstOrDefault(a => a.OperationName == "Consume TestMessage");
        activity.ShouldNotBeNull();
    }

    [Fact]
    public async Task SimulateReceive_SingleGenericType_ShouldInvokeConsumer()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("inferred-message"));

        var messages = testKit.GetSentMessages<ResponseMessage>();
        messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SimulateReceive_SingleGenericType_ShouldThrow_WhenConsumerDoesNotHandleMessageType()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(IncompatibleConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            testKit.SimulateReceiveAsync<IncompatibleConsumer>(new TestMessage("incompatible")));
    }

    [Fact]
    public async Task SimulateReceive_NonGeneric_ShouldAutoDispatchByMessageType()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(AutoDispatchConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync(new AutoDispatchMessage("auto-dispatched"));

        var messages = testKit.GetSentMessages<ResponseMessage>();
        messages.Count.ShouldBe(1);
        messages[0].Content.ShouldBe("Auto: auto-dispatched");
    }

    [Fact]
    public async Task SimulateReceive_NonGeneric_ShouldDispatchToAllMatchingConsumers()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(FirstBroadcastConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync(new BroadcastMessage("hello broadcast"));

        var messages = testKit.GetSentMessages<ResponseMessage>();
        messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SimulateReceive_NonGeneric_ShouldThrow_WhenNoConsumerFound()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            testKit.SimulateReceiveAsync(new UnregisteredMessage("unhandled")));
    }

    [Fact]
    public async Task ShouldHavePublished_WithoutPredicate_ShouldReturnMessage_WhenMessageExists()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("hello"));

        var published = testKit.ShouldHavePublished<ResponseMessage>();
        published.Content.ShouldBe("Received: hello");
    }

    [Fact]
    public async Task ShouldHavePublished_WithoutPredicate_ShouldThrow_WhenNoMessagePublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c.AddBroker("test-broker", _ => { }));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        Should.Throw<InvalidOperationException>(() =>
            testKit.ShouldHavePublished<ResponseMessage>());
    }

    [Fact]
    public async Task ShouldHavePublished_WithPredicate_ShouldReturnMatchingMessage_WhenMatchingMessageExists()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("target-msg"));

        var published = testKit.ShouldHavePublished<ResponseMessage>(m => m.Content.Contains("target-msg"));
        published.ShouldNotBeNull();
    }

    [Fact]
    public async Task ShouldHavePublished_WithPredicate_ShouldThrow_WhenNoMatchingMessage()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("hello"));

        Should.Throw<InvalidOperationException>(() =>
            testKit.ShouldHavePublished<ResponseMessage>(m => m.Content == "non-existent"));
    }

    [Fact]
    public async Task ShouldNotHavePublished_WithoutPredicate_ShouldSucceed_WhenNoMessagePublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c.AddBroker("test-broker", _ => { }));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        testKit.ShouldNotHavePublished<ResponseMessage>();
    }

    [Fact]
    public async Task ShouldNotHavePublished_WithoutPredicate_ShouldThrow_WhenMessagePublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("hello"));

        Should.Throw<InvalidOperationException>(() =>
            testKit.ShouldNotHavePublished<ResponseMessage>());
    }

    [Fact]
    public async Task ShouldNotHavePublished_WithPredicate_ShouldSucceed_WhenNoMatchingMessagePublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("hello"));

        testKit.ShouldNotHavePublished<ResponseMessage>(m => m.Content == "different-content");
    }

    [Fact]
    public async Task ShouldNotHavePublished_WithPredicate_ShouldThrow_WhenMatchingMessagePublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("matching"));

        Should.Throw<InvalidOperationException>(() =>
            testKit.ShouldNotHavePublished<ResponseMessage>(m => m.Content.Contains("matching")));
    }

    [Fact]
    public async Task WaitForPublishedMessageAsync_ShouldReturnExistingMessageImmediately()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("existing"));

        var msg = await testKit.WaitForPublishedMessageAsync<ResponseMessage>(m => m.Content.Contains("existing"));
        msg.Content.ShouldBe("Received: existing");
    }

    [Fact]
    public async Task WaitForPublishedMessageAsync_ShouldWaitForConcurrentlyPublishedMessage()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        var waitTask = testKit.WaitForPublishedMessageAsync<ResponseMessage>(
            predicate: m => m.Content.Contains("delayed"),
            timeout: TimeSpan.FromSeconds(3));

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            var publisher = serviceProvider.GetRequiredService<IPublisher<ResponseMessage>>();
            await publisher.PublishAsync(new ResponseMessage("delayed-result"));
        });

        var result = await waitTask;
        result.Content.ShouldBe("delayed-result");
    }

    [Fact]
    public async Task WaitForPublishedMessageAsync_ShouldThrowTimeoutException_WhenMessageNotPublished()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c.AddBroker("test-broker", _ => { }));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await Should.ThrowAsync<TimeoutException>(() =>
            testKit.WaitForPublishedMessageAsync<ResponseMessage>(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllPublishedMessages()
    {
        var services = new ServiceCollection();
        services.AddCarotte(c => c
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(TestConsumer).Assembly));
        services.AddCarotteTestKit();

        await using var serviceProvider = services.BuildServiceProvider();
        var testKit = serviceProvider.GetRequiredService<CarotteTestKit>();

        await testKit.SimulateReceiveAsync<TestConsumer>(new TestMessage("hello"));
        testKit.Clear();

        testKit.GetSentMessages<ResponseMessage>().ShouldBeEmpty();
    }
}
