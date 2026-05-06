using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Carotte.Tests;

public class CarotteTestKitTests
{
    public record TestMessage(string Content);
    public record ResponseMessage(string Content);

    [Queue("test-queue", broker: "test-broker")]
    public class TestConsumer(IProducer<ResponseMessage> producer) : IConsumer<TestMessage>
    {
        public async Task HandleAsync(TestMessage message, CancellationToken cancellationToken = default)
        {
            await producer.SendAsync(new ResponseMessage($"Received: {message.Content}"), cancellationToken);
        }
    }

    public class NoAttributeConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task SimulateReceive_ShouldInvokeConsumer_AndStoreSentMessages()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCarotte(c =>
        {
            c.AddProducer<ResponseMessage>("broker1", "exchange1")
             .AddAssemblies(typeof(TestConsumer).Assembly);
        });
        services.AddCarotteTestKit();

        var sp = services.BuildServiceProvider();
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
    public async Task Producer_ShouldBeMockable_WithMoq()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCarotte(c =>
        {
            c.AddProducer<ResponseMessage>("broker1", "exchange1")
             .AddAssemblies(typeof(TestConsumer).Assembly);
        });
        services.AddCarotteTestKit();

        // Enregistrement explicite du mock
        services.AddMockProducer<ResponseMessage>();

        var sp = services.BuildServiceProvider();
        var testKit = sp.GetRequiredService<CarotteTestKit>();
        var mockProducer = sp.GetMockProducer<ResponseMessage>();

        var testMessage = new TestMessage("Mock Me");

        // Act
        await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(testMessage);

        // Assert
        mockProducer.Verify(p => p.SendAsync(It.Is<ResponseMessage>(r => r.Content == "Received: Mock Me"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseTestMode_OnServiceCollection_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCarotte(c =>
        {
            c.AddProducer<ResponseMessage>("broker1", "exchange1")
             .AddAssemblies(typeof(TestConsumer).Assembly);
        });

        // Act - Appel indépendant après AddCarotte
        services.AddCarotteTestKit();

        var sp = services.BuildServiceProvider();
        var testKit = sp.GetRequiredService<CarotteTestKit>();

        var testMessage = new TestMessage("Hello from ServiceCollection");

        // Act
        await testKit.SimulateReceiveAsync<TestConsumer, TestMessage>(testMessage);

        // Assert
        var sentMessages = testKit.GetSentMessages<ResponseMessage>();
        sentMessages.Count.ShouldBe(1);
        sentMessages[0].Content.ShouldBe("Received: Hello from ServiceCollection");
    }
}
