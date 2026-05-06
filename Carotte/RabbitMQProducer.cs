using Carotte.pipeline;

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
    private bool _initialized;

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var effectiveExchange = exchange;
        var routingKey = typeof(TMessage).Name;

        if (string.IsNullOrEmpty(effectiveExchange))
        {
            effectiveExchange = typeof(TMessage).Name.ToDefaultExchangeName();
            routingKey = string.Empty;
        }

        var context = new ProducerContext<TMessage>(message, effectiveExchange, routingKey, cancellationToken);

        if (!_initialized)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_initialized)
                {
                    await rabbitMqClient.ConnectAsync(broker, cancellationToken);

                    if (string.IsNullOrEmpty(exchange))
                    {
                        await rabbitMqClient.ExchangeDeclareAsync(
                            exchange: effectiveExchange,
                            type: "fanout",
                            durable: true,
                            autoDelete: false,
                            cancellationToken: cancellationToken);
                    }

                    _initialized = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
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
