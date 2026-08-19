namespace Carotte.Sample;

/// <summary>
/// Represents an event triggered when a new order is placed.
/// </summary>
/// <param name="OrderId">The unique identifier of the created order.</param>
/// <param name="CustomerName">The full name of the customer.</param>
/// <param name="Amount">The total monetary amount for the order.</param>
[Publisher("my-broker", "orders-exchange")]
public record OrderCreated(Guid OrderId, string CustomerName, decimal Amount);