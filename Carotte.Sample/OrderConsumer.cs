namespace Carotte.Sample;

public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);

[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
public partial class OrderConsumer(ILogger<OrderConsumer> logger) : Consumer, IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        LogTimeCommandeReçueIdOrderidClientCustomerMontantAmount(logger, DateTime.Now.ToLongTimeString(), message.OrderId, message.CustomerName, message.Amount);
        
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "[{time}] Commande reçue : ID={orderId}, Client={customer}, Montant={amount}€")]
    static partial void LogTimeCommandeReçueIdOrderidClientCustomerMontantAmount(ILogger<OrderConsumer> logger, string time, Guid orderId, string customer, decimal amount);
}
