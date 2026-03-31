using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte.pipeline;

public record ConsumerContext(
    IChannel Channel,
    BasicDeliverEventArgs DeliveryArgs,
    CancellationToken CancellationToken = default,
    object? Message = null,
    Type? MessageType = null);

public delegate Task ConsumerDelegate(ConsumerContext context);

public interface IConsumerMiddleware
{
    Task InvokeAsync(ConsumerContext context, ConsumerDelegate next);
}
