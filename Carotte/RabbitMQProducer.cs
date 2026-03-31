namespace Carotte;

public class RabbitMqProducer<TMessage> : IProducer<TMessage> where TMessage : class
{
    private readonly ProducerDelegate<TMessage> _pipeline;
    private readonly string _exchange;

    public RabbitMqProducer(IConnectionManager connectionManager, ISerializer serializer, string broker, string exchange)
    {
        _exchange = exchange;
        
        var middlewares = new List<IProducerMiddleware<TMessage>>
        {
            new ProducerMetricsMiddleware<TMessage>(),
            new ProducerTracingMiddleware<TMessage>(),
            new SerializationMiddleware<TMessage>(serializer),
            new RabbitMqPublishMiddleware<TMessage>(connectionManager, broker)
        };

        ProducerDelegate<TMessage> next = _ => Task.CompletedTask;

        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        _pipeline = next;
    }

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var routingKey = typeof(TMessage).Name;
        var context = new ProducerContext<TMessage>(message, _exchange, routingKey, cancellationToken);
        await _pipeline(context);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
