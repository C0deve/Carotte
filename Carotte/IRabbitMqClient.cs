using RabbitMQ.Client;

namespace Carotte;

public interface IRabbitMqClient : IAsyncDisposable
{
    ValueTask<IChannel> GetChannelAsync(string broker, CancellationToken cancellationToken = default);
    
    Task BasicPublishAsync<TMessage>(
        string broker,
        string exchange,
        string routingKey,
        byte[] body,
        BasicProperties properties,
        bool mandatory = true,
        CancellationToken cancellationToken = default) where TMessage : class;

    Task BasicAckAsync(string broker, ulong deliveryTag, bool multiple = false, CancellationToken cancellationToken = default);
    
    Task BasicNackAsync(string broker, ulong deliveryTag, bool multiple = false, bool requeue = true, CancellationToken cancellationToken = default);

    Task<string> BasicConsumeAsync(
        string broker,
        string queue,
        bool autoAck,
        string consumerTag,
        bool noLocal,
        bool exclusive,
        IDictionary<string, object?>? arguments,
        AsyncDefaultBasicConsumer consumer,
        CancellationToken cancellationToken = default);

    Task QueueDeclareAsync(
        string broker,
        string queue,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task ExchangeDeclareAsync(
        string broker,
        string exchange,
        string type = "topic",
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task QueueBindAsync(
        string broker,
        string queue,
        string exchange,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default);
}
