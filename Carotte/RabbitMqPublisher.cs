using Carotte.Pipeline;
using Microsoft.Extensions.Logging;

namespace Carotte;

/// <summary>
/// Default implementation of <see cref="IPublisher{TMessage}"/> managing message publication to RabbitMQ.
/// Encapsulates lazy connection management, exchange declaration, and the execution of the publication pipeline.
/// </summary>
/// <typeparam name="TMessage">The type of the message to publish.</typeparam>
internal sealed class RabbitMqPublisher<TMessage>(
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    ILogger<RabbitMqPublisher<TMessage>> logger,
    string broker,
    string exchange,
    string routingKey,
    ExchangeType exchangeType,
    bool declareExchange,
    bool durable,
    bool autoDelete,
    IMessageTypeResolver? messageTypeResolver = null)
    : IPublisher<TMessage>, IAsyncDisposable
{
    private readonly IMessageTypeResolver _messageTypeResolver = messageTypeResolver ?? MessageTypeResolver.Default;

    // Assembled publication pipeline (Metrics -> Tracing -> Serialization -> RabbitMQ publish)
    private readonly PublisherPipeline<TMessage> _pipeline = new PublisherPipelineBuilder<TMessage>()
        .Use(new PublisherMetricsMiddleware<TMessage>())
        .Use(new PublisherTracingMiddleware<TMessage>())
        .Use(new SerializationMiddleware<TMessage>(serializer))
        .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient))
        .Build();

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of <see cref="RabbitMqPublisher{TMessage}"/> with default convention-based topology.
    /// </summary>
    public RabbitMqPublisher(
        IRabbitMqClient rabbitMqClient,
        ISerializer serializer,
        ILogger<RabbitMqPublisher<TMessage>> logger,
        string broker,
        string? exchange,
        IMessageTypeResolver? messageTypeResolver = null)
        : this(
            rabbitMqClient,
            serializer,
            logger,
            broker,
            string.IsNullOrWhiteSpace(exchange) ? typeof(TMessage).Name.ToDefaultExchangeName() : exchange,
            string.IsNullOrWhiteSpace(exchange) ? string.Empty : typeof(TMessage).Name,
            string.IsNullOrWhiteSpace(exchange) ? ExchangeType.Fanout : ExchangeType.Direct,
            declareExchange: true,
            durable: true,
            autoDelete: false,
            messageTypeResolver)
    {
    }

    /// <inheritdoc/>
    public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        // Thread-safe lazy initialization of connection and exchange
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

        // Create publisher context with resolved message type header and execute pipeline
        var context = new PublisherContext<TMessage>(
            message,
            exchange,
            routingKey,
            _messageTypeResolver.GetTypeIdentifier(typeof(TMessage)),
            cancellationToken);
        await _pipeline.ExecuteAsync(context);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await rabbitMqClient.DisposeAsync();
        _semaphore.Dispose();
    }
}
