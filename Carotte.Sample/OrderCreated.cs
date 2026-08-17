namespace Carotte.Sample;

[Publisher("my-broker", "orders-exchange")]
public record OrderCreated(Guid OrderId, string CustomerName, decimal Amount);