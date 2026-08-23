using Carotte.Sample.Contracts;

namespace Carotte.Sample.Consumers;

/// <summary>
/// Multi-event consumer auditing the complete order lifecycle.
/// </summary>
public sealed partial class OrderAuditConsumer(
    ILogger<OrderAuditConsumer> logger) :
    IConsumer<OrderPlacedEvent>,
    IConsumer<OrderProcessedEvent>,
    IConsumer<OrderCancelledEvent>
{
    public Task HandleAsync(OrderPlacedEvent message, CancellationToken cancellationToken)
    {
        LogAuditPlaced(logger, message.OrderId, message.TotalAmount);
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderProcessedEvent message, CancellationToken cancellationToken)
    {
        LogAuditProcessed(logger, message.OrderId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderCancelledEvent message, CancellationToken cancellationToken)
    {
        LogAuditCancelled(logger, message.OrderId, message.Reason);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "[AUDIT] Order placed: {OrderId}, Amount={Amount}€")]
    private static partial void LogAuditPlaced(ILogger<OrderAuditConsumer> logger, Guid orderId, decimal amount);

    [LoggerMessage(LogLevel.Information, "[AUDIT] Order successfully processed: {OrderId}")]
    private static partial void LogAuditProcessed(ILogger<OrderAuditConsumer> logger, Guid orderId);

    [LoggerMessage(LogLevel.Information, "[AUDIT] Order cancelled: {OrderId}, Reason={Reason}")]
    private static partial void LogAuditCancelled(ILogger<OrderAuditConsumer> logger, Guid orderId, string reason);
}
