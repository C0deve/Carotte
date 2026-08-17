namespace Carotte.Sample;

[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
public partial class OrderConsumer(IPublisher<NotificationMessage> publisher, ILogger<OrderConsumer> logger) : IConsumer<OrderCreated>
{
    public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        LogTimeOrderReceivedIdOrderidClientCustomerAmountAmount(logger, DateTime.Now.ToLongTimeString(), message.OrderId, message.CustomerName, message.Amount);
        await publisher.PublishAsync(new NotificationMessage(message.OrderId, $"Order received for {message.CustomerName}", "client@example.com"), cancellationToken);
    }

    [LoggerMessage(LogLevel.Information, "[{time}] Order received: ID={orderId}, Client={customer}, Amount={amount}€")]
    static partial void LogTimeOrderReceivedIdOrderidClientCustomerAmountAmount(ILogger<OrderConsumer> logger, string time, Guid orderId, string customer, decimal amount);
}
