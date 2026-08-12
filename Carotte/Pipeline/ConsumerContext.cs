using RabbitMQ.Client.Events;

namespace Carotte.pipeline;

internal record ConsumerContext(
    BasicDeliverEventArgs DeliveryArgs,
    IServiceProvider Services,
    object? Message = null,
    Type? MessageType = null,
    CancellationToken CancellationToken = default);