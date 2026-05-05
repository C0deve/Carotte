using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte;

public sealed class RabbitMqClient(IConnectionManager connectionManager, ILogger<RabbitMqClient> logger) : IRabbitMqClient
{
    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _brokerName;

    public event AsyncEventHandler<BasicDeliverEventArgs>? ReceivedAsync;

    public async Task ConnectAsync(string brokerName, CancellationToken cancellationToken = default)
    {
        if (_channel != null) return;
        _brokerName = brokerName;
        await connectionManager.RegisterHostAsync(brokerName);
        var connection = await connectionManager.GetConnectionAsync(brokerName);
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.ReceivedAsync += (sender, ea) => ReceivedAsync?.Invoke(sender, ea) ?? Task.CompletedTask;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);

            if (_brokerName != null)
            {
                await connectionManager.UnregisterHostAsync(_brokerName);
                _brokerName = null;
            }
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    public async Task BasicPublishAsync<TMessage>(
        string exchange,
        string routingKey,
        byte[] body,
        BasicProperties properties,
        bool mandatory = true,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        logger.LogPublishingMessage(typeof(TMessage).Name, exchange, routingKey, string.Empty);
        await _channel.BasicPublishAsync(
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
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        if (_consumer == null) throw new InvalidOperationException("Consumer not initialized.");
        logger.LogStartingConsumptionOnQueue(queue, string.Empty);
        return await _channel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal, exclusive, arguments, _consumer, cancellationToken);
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
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        logger.LogDeclaringQueue(queue, string.Empty);
        await _channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments, passive, noWait, cancellationToken);
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
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        logger.LogDeclaringExchange(exchange, string.Empty);
        await _channel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments, passive, noWait, cancellationToken);
    }

    public async Task QueueBindAsync(
        string queue,
        string exchange,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        logger.LogBindingQueueToExchange(queue, exchange, routingKey, string.Empty);
        await _channel.QueueBindAsync(queue, exchange, routingKey, arguments, noWait, cancellationToken);
    }

    public async Task ExchangeBindAsync(
        string destination,
        string source,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        logger.LogBindingExchangeToExchange(destination, source, routingKey, string.Empty);
        await _channel.ExchangeBindAsync(destination, source, routingKey, arguments, noWait, cancellationToken);
    }

    public async Task BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
    {
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        await _channel.BasicAckAsync(deliveryTag, multiple, cancellationToken);
    }

    public async Task BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default)
    {
        if (_channel == null) throw new InvalidOperationException("Client not initialized.");
        await _channel.BasicNackAsync(deliveryTag, multiple, requeue, cancellationToken);
    }
}
