using RabbitMQ.Client;

namespace Carotte;

public record ProducerContext<TMessage>(
    TMessage Message,
    string Exchange,
    string RoutingKey,
    CancellationToken CancellationToken = default) where TMessage : class
{
    public BasicProperties? Properties { get; set; }
    public byte[]? Body { get; set; }
}

public delegate Task ProducerDelegate<TMessage>(ProducerContext<TMessage> context) where TMessage : class;

public interface IProducerMiddleware<TMessage> where TMessage : class
{
    Task InvokeAsync(ProducerContext<TMessage> context, ProducerDelegate<TMessage> next);
}
