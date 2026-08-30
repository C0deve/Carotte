using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte;

public interface IRabbitMqClient : IAsyncDisposable
{
    event AsyncEventHandler<BasicDeliverEventArgs>? ReceivedAsync;
    Task ConnectAsync(string brokerName, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);

    Task BasicPublishAsync<TMessage>(
        string exchange,
        string routingKey,
        byte[] body,
        BasicProperties properties,
        bool mandatory = true,
        CancellationToken cancellationToken = default);

    Task<string> BasicConsumeAsync(
        string queue,
        bool autoAck,
        string consumerTag,
        bool noLocal,
        bool exclusive,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);

    Task QueueDeclareAsync(
        string queue,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task ExchangeDeclareAsync(
        string exchange,
        string type = "topic",
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task QueueBindAsync(
        string queue,
        string exchange,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task ExchangeBindAsync(
        string destination,
        string source,
        string routingKey = "",
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default);

    Task BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default);
    Task BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default);
    Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default);
}
