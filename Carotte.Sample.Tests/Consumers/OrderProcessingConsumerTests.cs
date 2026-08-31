using Carotte.Sample.Consumers;
using Carotte.Sample.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte.Sample.Tests.Consumers;

public sealed class OrderProcessingConsumerTests
{
    [Fact]
    public async Task HandleAsync_WhenOrderPlaced_ShouldPublishOrderProcessedEvent()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddCarotte(c => c
                .AddBroker("primary-broker", _ => { })
                .ScanAssemblies(typeof(OrderProcessingConsumer).Assembly))
            .AddCarotteTestKit()
            .BuildServiceProvider();

        var testKit = services.GetRequiredService<CarotteTestKit>();
        var orderEvent = new OrderPlacedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            99.99m,
            DateTimeOffset.UtcNow);

        // Act
        await testKit.ConsumeAsync(orderEvent);

        // Assert
        testKit.ShouldHavePublished<OrderProcessedEvent>(e => e.OrderId == orderEvent.OrderId);
    }
}
