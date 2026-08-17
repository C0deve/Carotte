namespace Carotte.Sample;

[Publisher("my-broker", "notifications-exchange")]
public record NotificationMessage(Guid OrderId, string Message, string RecipientEmail);

[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
public partial class MultiMessageConsumer(ILogger<MultiMessageConsumer> logger) : IConsumer<OrderCreated>, IConsumer<NotificationMessage>
{
    public Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        LogOrderReceived(logger, message.OrderId, message.CustomerName);
        return Task.CompletedTask;
    }

    public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        LogNotificationReceived(logger, message.OrderId, message.RecipientEmail);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "[Multi] Order received: ID={orderId}, Client={customer}")]
    static partial void LogOrderReceived(ILogger<MultiMessageConsumer> logger, Guid orderId, string customer);
    
    [LoggerMessage(LogLevel.Information, "[Multi] Notification received for order: ID={orderId}, Recipient={recipient}")]
    static partial void LogNotificationReceived(ILogger<MultiMessageConsumer> logger, Guid orderId, string recipient);
}
