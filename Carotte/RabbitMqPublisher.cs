using Carotte.pipeline;
using Microsoft.Extensions.Logging;

namespace Carotte;

public class RabbitMqPublisher<TMessage>(
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    ILogger<RabbitMqPublisher<TMessage>> logger,
    string broker,
    string exchange,
    string routingKey,
    ExchangeType exchangeType,
    bool declareExchange,
    bool durable,
    bool autoDelete)
    : IPublisher<TMessage>, IAsyncDisposable
    where TMessage : class
{
    private readonly PublisherPipeline<TMessage> _pipeline = new PublisherPipelineBuilder<TMessage>()
        .Use(new PublisherMetricsMiddleware<TMessage>())
        .Use(new PublisherTracingMiddleware<TMessage>())
        .Use(new SerializationMiddleware<TMessage>(serializer))
        .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient))
        .Build();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _initialized;

    public RabbitMqPublisher(
        IRabbitMqClient rabbitMqClient,
        ISerializer serializer,
        ILogger<RabbitMqPublisher<TMessage>> logger,
        string broker,
        string? exchange)
        : this(
            rabbitMqClient,
            serializer,
            logger,
            broker,
            string.IsNullOrWhiteSpace(exchange) ? typeof(TMessage).Name.ToDefaultExchangeName() : exchange,
            string.IsNullOrWhiteSpace(exchange) ? string.Empty : typeof(TMessage).Name,
            string.IsNullOrWhiteSpace(exchange) ? ExchangeType.Fanout : ExchangeType.Direct,
            string.IsNullOrWhiteSpace(exchange),
            durable: true,
            autoDelete: false)
    {
    }

    public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_initialized)
                {
                    await rabbitMqClient.ConnectAsync(broker, cancellationToken);

                    if (declareExchange)
                    {
                        await rabbitMqClient.ExchangeDeclareAsync(
                            exchange: exchange,
                            type: exchangeType.ToString().ToLowerInvariant(),
                            durable: durable,
                            autoDelete: autoDelete,
                            cancellationToken: cancellationToken);
                    }

                    _initialized = true;
                    logger.LogStartingRabbitmqPublisher(typeof(TMessage).Name, broker, exchange);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        var context = new PublisherContext<TMessage>(message, exchange, routingKey, cancellationToken);
        await _pipeline.ExecuteAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        await rabbitMqClient.DisposeAsync();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
