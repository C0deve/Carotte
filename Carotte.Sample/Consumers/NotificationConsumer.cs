using Carotte.Sample.Contracts;

namespace Carotte.Sample.Consumers;

/// <summary>
/// Consumes <see cref="OrderProcessedEvent"/> and sends notifications to customers.
/// </summary>
public sealed partial class NotificationConsumer(
    ILogger<NotificationConsumer> logger) : IConsumer<OrderProcessedEvent>
{
    public Task HandleAsync(OrderProcessedEvent message, CancellationToken cancellationToken)
    {
        LogNotificationSent(logger, message.OrderId, message.CustomerEmail);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Notification sent for processed order {OrderId} to {Email}")]
    private static partial void LogNotificationSent(ILogger<NotificationConsumer> logger, Guid orderId, string email);
}
