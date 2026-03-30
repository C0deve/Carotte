namespace Carotte.Sample;

public record NotificationMessage(Guid OrderId, string Message, string RecipientEmail);

[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
[Queue("notifications-queue", broker: "my-broker", exchange: "notifications-exchange", routingKey: "notification.sent")]
public partial class MultiMessageConsumer(ILogger<MultiMessageConsumer> logger) : Consumer, IConsumer<OrderCreatedMessage>, IConsumer<NotificationMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        LogOrderReceived(logger, message.OrderId, message.CustomerName);
        return Task.CompletedTask;
    }

    public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        LogNotificationReceived(logger, message.OrderId, message.RecipientEmail);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "[Multi] Commande reçue : ID={OrderId}, Client={Customer}")]
    static partial void LogOrderReceived(ILogger<MultiMessageConsumer> logger, Guid OrderId, string Customer);

    [LoggerMessage(LogLevel.Information, "[Multi] Notification reçue pour commande : ID={OrderId}, Destinataire={Recipient}")]
    static partial void LogNotificationReceived(ILogger<MultiMessageConsumer> logger, Guid OrderId, string Recipient);
}
