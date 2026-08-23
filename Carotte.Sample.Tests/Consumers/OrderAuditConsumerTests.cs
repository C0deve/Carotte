using Carotte.Sample.Consumers;
using Carotte.Sample.Contracts;
using Microsoft.Extensions.Logging;
using Moq;

namespace Carotte.Sample.Tests.Consumers;

public sealed class OrderAuditConsumerTests
{
    [Fact]
    public async Task HandleAsync_WhenOrderPlaced_ShouldHandleSuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<OrderAuditConsumer>>();
        var consumer = new OrderAuditConsumer(loggerMock.Object);
        var message = new OrderPlacedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            120m,
            DateTimeOffset.UtcNow);

        // Act
        var handleTask = consumer.HandleAsync(message, CancellationToken.None);

        // Assert
        await handleTask;
    }

    [Fact]
    public async Task HandleAsync_WhenOrderProcessed_ShouldHandleSuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<OrderAuditConsumer>>();
        var consumer = new OrderAuditConsumer(loggerMock.Object);
        var message = new OrderProcessedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            120m,
            DateTimeOffset.UtcNow);

        // Act
        var handleTask = consumer.HandleAsync(message, CancellationToken.None);

        // Assert
        await handleTask;
    }

    [Fact]
    public async Task HandleAsync_WhenOrderCancelled_ShouldHandleSuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<OrderAuditConsumer>>();
        var consumer = new OrderAuditConsumer(loggerMock.Object);
        var message = new OrderCancelledEvent(
            Guid.NewGuid(),
            "Out of stock",
            DateTimeOffset.UtcNow);

        // Act
        var handleTask = consumer.HandleAsync(message, CancellationToken.None);

        // Assert
        await handleTask;
    }
}
