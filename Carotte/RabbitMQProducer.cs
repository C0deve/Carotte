using Carotte.pipeline;
using RabbitMQ.Client;

namespace Carotte;

public class RabbitMqProducer<TMessage>(
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    string broker,
    string exchange)
    : IProducer<TMessage>
    where TMessage : class
{
    private readonly ProducerPipeline<TMessage> _pipeline = new ProducerPipelineBuilder<TMessage>()
        .Use(new ProducerMetricsMiddleware<TMessage>())
        .Use(new ProducerTracingMiddleware<TMessage>())
        .Use(new SerializationMiddleware<TMessage>(serializer))
        .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient, broker))
        .Build();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var routingKey = typeof(TMessage).Name;
        var context = new ProducerContext<TMessage>(message, exchange, routingKey, cancellationToken);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await rabbitMqClient.ConnectAsync(broker, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }

        await _pipeline.ExecuteAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        await rabbitMqClient.DisposeAsync();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
