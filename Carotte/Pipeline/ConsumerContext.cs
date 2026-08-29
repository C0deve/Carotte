using RabbitMQ.Client.Events;

namespace Carotte.pipeline;

/// <summary>
/// Context passed through the consumer middleware pipeline, carrying RabbitMQ delivery arguments,
/// scoped service provider, deserialized message, and resolved type metadata.
/// </summary>
/// <param name="DeliveryArgs">Raw delivery event arguments received from RabbitMQ.</param>
/// <param name="Services">Scoped service provider created for processing this specific message.</param>
/// <param name="Message">Deserialized .NET message payload.</param>
/// <param name="MessageType">Resolved .NET type of the message.</param>
/// <param name="CancellationToken">Cancellation token for consumer host shutdown.</param>
internal record ConsumerContext(
    BasicDeliverEventArgs DeliveryArgs,
    IServiceProvider Services,
    object? Message = null,
    Type? MessageType = null,
    CancellationToken CancellationToken = default);