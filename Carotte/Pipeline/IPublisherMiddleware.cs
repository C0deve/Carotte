using RabbitMQ.Client;

namespace Carotte.pipeline;

internal record PublisherContext<TMessage>(
    TMessage Message,
    string Exchange,
    string RoutingKey,
    CancellationToken CancellationToken = default) where TMessage : class
{
    public BasicProperties? Properties { get; set; }
    public byte[]? Body { get; set; }
}

internal delegate Task PublisherDelegate<TMessage>(PublisherContext<TMessage> context) where TMessage : class;

internal interface IPublisherMiddleware<TMessage> where TMessage : class
{
    Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next);
}
