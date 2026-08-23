namespace Carotte.Sample.Contracts;

/// <summary>
/// Represents an event published when a customer places a new order.
/// </summary>
/// <param name="OrderId">The unique identifier of the order.</param>
/// <param name="CustomerId">The unique identifier of the customer.</param>
/// <param name="CustomerEmail">The email address of the customer.</param>
/// <param name="TotalAmount">The total amount of the order.</param>
/// <param name="PlacedAt">The timestamp when the order was placed.</param>
[Publisher]
public sealed record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    DateTimeOffset PlacedAt);
