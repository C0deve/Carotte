using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Carotte.Tests;

public class CarotteTestKitTests
{
    public record TestMessage(string Content);

    [Publisher]
    public record ResponseMessage(string Content);

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

    [Fact]
    public async Task SimulateReceive_ShouldInvokeConsumer_AndStoreSentMessages()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddCarotte(c => c
                .AddBroker("test-broker", _ => { })
                .AddAssemblies(typeof(TestConsumer).Assembly))
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
                .AddAssemblies(typeof(TestConsumer).Assembly))
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
            .AddAssemblies(typeof(ScopedConsumer).Assembly));
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
}
