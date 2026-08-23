namespace Carotte.Sample.Contracts;

/// <summary>
/// Represents an event published when an order has been successfully processed.
/// </summary>
/// <param name="OrderId">The unique identifier of the order.</param>
/// <param name="CustomerId">The unique identifier of the customer.</param>
/// <param name="CustomerEmail">The email address of the customer.</param>
/// <param name="TotalAmount">The total amount processed.</param>
/// <param name="ProcessedAt">The timestamp when the order was processed.</param>
[Publisher]
public sealed record OrderProcessedEvent(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    DateTimeOffset ProcessedAt);
