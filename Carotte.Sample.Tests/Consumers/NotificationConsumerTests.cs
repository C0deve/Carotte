using Carotte.Sample.Consumers;
using Carotte.Sample.Contracts;
using Microsoft.Extensions.Logging;
using Moq;

namespace Carotte.Sample.Tests.Consumers;

public sealed class NotificationConsumerTests
{
    [Fact]
    public async Task HandleAsync_WhenOrderProcessed_ShouldHandleWithoutErrors()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NotificationConsumer>>();
        var consumer = new NotificationConsumer(loggerMock.Object);
        var orderProcessed = new OrderProcessedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            49.99m,
            DateTimeOffset.UtcNow);

        // Act
        var handleTask = consumer.HandleAsync(orderProcessed, CancellationToken.None);

        // Assert
        await handleTask;
    }
}
