namespace Carotte.Sample;

[Publisher("my-broker", "orders-exchange")]
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);

[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
public partial class OrderConsumer(ILogger<OrderConsumer> logger) : IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        LogTimeOrderReceivedIdOrderidClientCustomerAmountAmount(logger, DateTime.Now.ToLongTimeString(), message.OrderId, message.CustomerName, message.Amount);
        
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "[{time}] Order received: ID={orderId}, Client={customer}, Amount={amount}€")]
    static partial void LogTimeOrderReceivedIdOrderidClientCustomerAmountAmount(ILogger<OrderConsumer> logger, string time, Guid orderId, string customer, decimal amount);
}
