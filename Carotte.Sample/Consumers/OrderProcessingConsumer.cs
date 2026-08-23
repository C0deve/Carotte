using Carotte.Sample.Contracts;

namespace Carotte.Sample.Consumers;

/// <summary>
/// Consumes <see cref="OrderPlacedEvent"/>, performs business validation and processing, and publishes <see cref="OrderProcessedEvent"/>.
/// </summary>
public sealed partial class OrderProcessingConsumer(
    IPublisher<OrderProcessedEvent> publisher,
    ILogger<OrderProcessingConsumer> logger) : IConsumer<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent message, CancellationToken cancellationToken)
    {
        LogOrderReceived(logger, message.OrderId, message.CustomerId, message.TotalAmount);

        var processedEvent = new OrderProcessedEvent(
            message.OrderId,
            message.CustomerId,
            message.CustomerEmail,
            message.TotalAmount,
            DateTimeOffset.UtcNow);

        await publisher.PublishAsync(processedEvent, cancellationToken);
    }

    [LoggerMessage(LogLevel.Information, "Processing order {OrderId} for customer {CustomerId} (Amount: {Amount}€)")]
    private static partial void LogOrderReceived(ILogger<OrderProcessingConsumer> logger, Guid orderId, Guid customerId, decimal amount);
}
