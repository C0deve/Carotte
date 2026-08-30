namespace Carotte.Documentation.Tests;

[Published("primary-broker", "orders.exchange", routingKey: "order.created", exchangeType: ExchangeType.Topic)]
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount, DateTime CreatedAtUtc);

[Published("primary-broker")]
public record DefaultConventionMessage(string Id, string Content);

[Queue("orders-queue", broker: "primary-broker", exchange: "orders.exchange", routingKey: "order.created", maxRetryAttempts: 5, deadLetterExchange: "orders.dlx")]
public class OrderCreatedConsumer : IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ConventionConsumer : IConsumer<DefaultConventionMessage>
{
    public Task HandleAsync(DefaultConventionMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

[Queue("multi-queue", broker: "primary-broker")]
[Binding("events.exchange", "order.*", exchangeType: ExchangeType.Topic)]
[Binding("notifications.exchange", "order.notify")]
public class MultiBindingConsumer : IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
