using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Carotte;

internal sealed class RabbitMqClient(IConnectionManager connectionManager, ILogger<RabbitMqClient> logger) : IRabbitMqClient
{
    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _brokerName;
    private bool _registeredHost;

    public event AsyncEventHandler<BasicDeliverEventArgs>? ReceivedAsync;

    public async Task ConnectAsync(string brokerName, CancellationToken cancellationToken = default)
    {
        if (_channel is { IsOpen: true })
        {
            if (_brokerName != brokerName)
            {
                throw new InvalidOperationException($"RabbitMqClient is already connected to broker '{_brokerName}'.");
            }

            return;
        }

        _brokerName = brokerName;
        await EnsureChannelAsync(cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await CloseChannelAndDisposeAsync(cancellationToken);

        await CloseHostConnectionAsync();

        _brokerName = null;
    }

    private async Task CloseHostConnectionAsync()
    {
        if (_registeredHost && _brokerName != null)
        {
            try
            {
                await connectionManager.UnregisterHostAsync(_brokerName);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or AlreadyClosedException)
            {
            }

            _registeredHost = false;
        }
    }

    private async Task CloseChannelAndDisposeAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            try
            {
                if (_channel.IsOpen)
                {
                    await _channel.CloseAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or AlreadyClosedException)
            {
            }

            try
            {
                await _channel.DisposeAsync();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or AlreadyClosedException)
            {
            }

            _channel = null;
            _consumer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    public async Task BasicPublishAsync<TMessage>(
        string exchange,
        string routingKey,
        byte[] body,
        BasicProperties properties,
        bool mandatory = true,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        logger.LogPublishingMessage(typeof(TMessage).Name, exchange, routingKey, string.Empty);
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: mandatory,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task<string> BasicConsumeAsync(
        string queue,
        bool autoAck,
        string consumerTag,
        bool noLocal,
        bool exclusive,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        if (_consumer == null) throw new InvalidOperationException("Consumer not initialized.");
        logger.LogStartingConsumptionOnQueue(queue, string.Empty);
        return await channel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal, exclusive, arguments, _consumer, cancellationToken);
    }

    public async Task QueueDeclareAsync(
        string queue,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        logger.LogDeclaringQueue(queue, string.Empty);
        await channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments, passive, noWait, cancellationToken);
    }

    public async Task ExchangeDeclareAsync(
        string exchange,
        string type = "topic",
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        logger.LogDeclaringExchange(exchange, string.Empty);
        await channel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments, passive, noWait, cancellationToken);
    }

    public Task ExchangeDeclareAsync(
        string exchange,
        ExchangeType type,
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default) =>
        ExchangeDeclareAsync(
            exchange,
            type.ToString().ToLowerInvariant(),
            durable,
            autoDelete,
            arguments,
            passive,
            noWait,
            cancellationToken);

    public async Task QueueBindAsync(
        string queue,
        string exchange,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        logger.LogBindingQueueToExchange(queue, exchange, routingKey, string.Empty);
        await channel.QueueBindAsync(queue, exchange, routingKey, arguments, noWait, cancellationToken);
    }

    public async Task ExchangeBindAsync(
        string destination,
        string source,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        logger.LogBindingExchangeToExchange(destination, source, routingKey, string.Empty);
        await channel.ExchangeBindAsync(destination, source, routingKey, arguments, noWait, cancellationToken);
    }

    public async Task BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        await channel.BasicAckAsync(deliveryTag, multiple, cancellationToken);
    }

    public async Task BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        await channel.BasicNackAsync(deliveryTag, multiple, requeue, cancellationToken);
    }

    public async Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAsync(cancellationToken);
        await channel.BasicQosAsync(prefetchSize, prefetchCount, global, cancellationToken);
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_brokerName == null)
        {
            throw new InvalidOperationException("Client not initialized.");
        }

        if (_channel != null)
        {
            await _channel.DisposeAsync();
            _channel = null;
            _consumer = null;
        }

        if (!_registeredHost)
        {
            await connectionManager.RegisterHostAsync(_brokerName);
            _registeredHost = true;
        }

        var connection = await connectionManager.GetConnectionAsync(_brokerName);
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.ReceivedAsync += (sender, ea) => ReceivedAsync?.Invoke(sender, ea) ?? Task.CompletedTask;

        return _channel;
    }
}
