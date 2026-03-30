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

    [LoggerMessage(LogLevel.Information, "[{Time}] Commande reçue : ID={OrderId}, Client={Customer}, Montant={Amount}€")]
    static partial void LogTimeCommandeReçueIdOrderidClientCustomerMontantAmount(ILogger<OrderConsumer> logger, string Time, Guid OrderId, string Customer, decimal Amount);
}
