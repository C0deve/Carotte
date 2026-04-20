using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Carotte;

public sealed class RabbitMqClient(IConnectionManager connectionManager, ILogger<RabbitMqClient> logger) : IRabbitMqClient
{
    private readonly Dictionary<string, IChannel> _channels = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async ValueTask<IChannel> GetChannelAsync(string broker, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_channels.TryGetValue(broker, out var channel) && channel.IsOpen)
            {
                return channel;
            }

            logger.LogCreatingNewChannelForBroker(broker);
            var connection = await connectionManager.GetConnectionAsync(broker);
            channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            _channels[broker] = channel;
            return channel;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task BasicPublishAsync<TMessage>(
        string broker,
        string exchange,
        string routingKey,
        byte[] body,
        BasicProperties properties,
        bool mandatory = true,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        logger.LogPublishingMessage(typeof(TMessage).Name, exchange, routingKey, broker);
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: mandatory,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task BasicAckAsync(string broker, ulong deliveryTag, bool multiple = false, CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.BasicAckAsync(deliveryTag, multiple, cancellationToken);
    }

    public async Task BasicNackAsync(string broker, ulong deliveryTag, bool multiple = false, bool requeue = true, CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.BasicNackAsync(deliveryTag, multiple, requeue, cancellationToken);
    }

    public async Task<string> BasicConsumeAsync(
        string broker,
        string queue,
        bool autoAck,
        string consumerTag,
        bool noLocal,
        bool exclusive,
        IDictionary<string, object?>? arguments,
        AsyncDefaultBasicConsumer consumer,
        CancellationToken cancellationToken = default)
    {
        logger.LogStartingConsumptionOnQueue(queue, broker);
        var channel = await GetChannelAsync(broker, cancellationToken);
        return await channel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer, cancellationToken);
    }

    public async Task QueueDeclareAsync(
        string broker,
        string queue,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogDeclaringQueue(queue, broker);
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments, passive, noWait, cancellationToken);
    }

    public async Task ExchangeDeclareAsync(
        string broker,
        string exchange,
        string type = "topic",
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogDeclaringExchange(exchange, broker);
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments, passive, noWait, cancellationToken);
    }

    public async Task QueueBindAsync(
        string broker,
        string queue,
        string exchange,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogBindingQueueToExchange(queue, exchange, routingKey, broker);
        var channel = await GetChannelAsync(broker, cancellationToken);
        await channel.QueueBindAsync(queue, exchange, routingKey, arguments, noWait, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (broker, channel) in _channels)
        {
            logger.LogDisposingChannelForBroker(broker);
            await channel.DisposeAsync();
        }
        _channels.Clear();
        _semaphore.Dispose();
    }
}
