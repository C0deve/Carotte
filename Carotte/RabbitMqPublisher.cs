using Carotte.pipeline;
using Microsoft.Extensions.Logging;

namespace Carotte;

public class RabbitMqPublisher<TMessage> : IPublisher<TMessage>, IAsyncDisposable
    where TMessage : class
{
    private readonly IRabbitMqClient _rabbitMqClient;
    private readonly ILogger<RabbitMqPublisher<TMessage>> _logger;
    private readonly string broker;
    private readonly string _exchange;
    private readonly string _routingKey;
    private readonly ExchangeType _exchangeType;
    private readonly bool _declareExchange;
    private readonly bool _durable;
    private readonly bool _autoDelete;
    private readonly PublisherPipeline<TMessage> _pipeline;
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

    public RabbitMqPublisher(
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
    {
        _rabbitMqClient = rabbitMqClient;
        _logger = logger;
        this.broker = broker;
        _exchange = exchange;
        _routingKey = routingKey;
        _exchangeType = exchangeType;
        _declareExchange = declareExchange;
        _durable = durable;
        _autoDelete = autoDelete;
        _pipeline = new PublisherPipelineBuilder<TMessage>()
            .Use(new PublisherMetricsMiddleware<TMessage>())
            .Use(new PublisherTracingMiddleware<TMessage>())
            .Use(new SerializationMiddleware<TMessage>(serializer))
            .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient, broker))
            .Build();
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
                    await _rabbitMqClient.ConnectAsync(broker, cancellationToken);

                    if (_declareExchange)
                    {
                        await _rabbitMqClient.ExchangeDeclareAsync(
                            exchange: _exchange,
                            type: _exchangeType.ToString().ToLowerInvariant(),
                            durable: _durable,
                            autoDelete: _autoDelete,
                            cancellationToken: cancellationToken);
                    }

                    _initialized = true;
                    _logger.LogStartingRabbitmqPublisher(typeof(TMessage).Name, broker, _exchange);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        var context = new PublisherContext<TMessage>(message, _exchange, _routingKey, cancellationToken);
        await _pipeline.ExecuteAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        await _rabbitMqClient.DisposeAsync();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
