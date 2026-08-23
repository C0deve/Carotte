using Carotte.Sample.Contracts;

namespace Carotte.Sample.Endpoints;

/// <summary>
/// Minimal API endpoints for managing orders and producing order events.
/// </summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (CreateOrderRequest request, IPublisher<OrderPlacedEvent> publisher, CancellationToken cancellationToken) =>
        {
            var orderId = Guid.NewGuid();
            var orderPlaced = new OrderPlacedEvent(
                orderId,
                request.CustomerId,
                request.Email,
                request.Amount,
                DateTimeOffset.UtcNow);

            await publisher.PublishAsync(orderPlaced, cancellationToken);
            return Results.Accepted($"/api/orders/{orderId}", new { OrderId = orderId });
        });

        group.MapPost("/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest request, IPublisher<OrderCancelledEvent> publisher, CancellationToken cancellationToken) =>
        {
            var orderCancelled = new OrderCancelledEvent(
                orderId,
                request.Reason,
                DateTimeOffset.UtcNow);

            await publisher.PublishAsync(orderCancelled, cancellationToken);
            return Results.Accepted();
        });

        return app;
    }

    /// <summary>
    /// Request payload for creating a new order.
    /// </summary>
    public sealed record CreateOrderRequest(Guid CustomerId, string Email, decimal Amount);

    /// <summary>
    /// Request payload for cancelling an existing order.
    /// </summary>
    public sealed record CancelOrderRequest(string Reason);
}
