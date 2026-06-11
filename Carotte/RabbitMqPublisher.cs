using Microsoft.Extensions.Logging;
using Carotte.pipeline;

namespace Carotte;

public class RabbitMqPublisher<TMessage>(
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    ILogger<RabbitMqPublisher<TMessage>> logger,
    string broker,
    string exchange)
    : IPublisher<TMessage>
    where TMessage : class
{
    private readonly PublisherPipeline<TMessage> _pipeline = new PublisherPipelineBuilder<TMessage>()
        .Use(new PublisherMetricsMiddleware<TMessage>())
        .Use(new PublisherTracingMiddleware<TMessage>())
        .Use(new SerializationMiddleware<TMessage>(serializer))
        .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient, broker))
        .Build();

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _initialized;

    public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var effectiveExchange = exchange;
        var routingKey = typeof(TMessage).Name;

        if (string.IsNullOrEmpty(effectiveExchange))
        {
            effectiveExchange = typeof(TMessage).Name.ToDefaultExchangeName();
            routingKey = string.Empty;
        }

        var context = new PublisherContext<TMessage>(message, effectiveExchange, routingKey, cancellationToken);

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
                        // Declare fanout exchange (convention)
                        await rabbitMqClient.ExchangeDeclareAsync(
                            exchange: effectiveExchange,
                            type: "fanout",
                            durable: true,
                            autoDelete: false,
                            cancellationToken: cancellationToken);
                    }

                    _initialized = true;
                    logger.LogStartingRabbitmqPublisher(typeof(TMessage).Name, broker, effectiveExchange);
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
