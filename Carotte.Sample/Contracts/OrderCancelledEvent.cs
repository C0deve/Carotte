namespace Carotte.Sample.Contracts;

/// <summary>
/// Represents an event published when an order is cancelled.
/// </summary>
/// <param name="OrderId">The unique identifier of the cancelled order.</param>
/// <param name="Reason">The cancellation reason.</param>
/// <param name="CancelledAt">The timestamp when the order was cancelled.</param>
[Published]
public sealed record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    DateTimeOffset CancelledAt);
