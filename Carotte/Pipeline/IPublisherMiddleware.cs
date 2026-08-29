using RabbitMQ.Client;

namespace Carotte.pipeline;

/// <summary>
/// Context passed through the publisher middleware pipeline, containing message payload,
/// target exchange, routing key, AMQP basic properties, serialized body, and cancellation token.
/// </summary>
/// <typeparam name="TMessage">The type of message being published.</typeparam>
internal record PublisherContext<TMessage>(
    TMessage Message,
    string Exchange,
    string RoutingKey,
    string? TypeIdentifier = null,
    CancellationToken CancellationToken = default) where TMessage : class
{
    /// <summary>
    /// AMQP message properties, initialized with resolved type identifier.
    /// </summary>
    public BasicProperties Properties { get; set; } = new()
    {
        Type = TypeIdentifier ?? MessageTypeResolver.Default.GetTypeIdentifier(typeof(TMessage))
    };

    /// <summary>
    /// Serialized byte payload to publish to the broker.
    /// </summary>
    public byte[]? Body { get; set; }
}

/// <summary>
/// Delegate representing the next step in the publisher pipeline.
/// </summary>
internal delegate Task PublisherDelegate<TMessage>(PublisherContext<TMessage> context) where TMessage : class;

/// <summary>
/// Middleware interface for intercepting outgoing messages in the publisher pipeline.
/// </summary>
/// <typeparam name="TMessage">The type of message being published.</typeparam>
internal interface IPublisherMiddleware<TMessage> where TMessage : class
{
    /// <summary>
    /// Invokes the middleware logic and passes control to the next delegate in the pipeline.
    /// </summary>
    Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next);
}
