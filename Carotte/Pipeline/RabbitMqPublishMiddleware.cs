using RabbitMQ.Client;

namespace Carotte.pipeline;

public class RabbitMqPublishMiddleware<TMessage>(IConnectionManager connectionManager, string broker) : IProducerMiddleware<TMessage> where TMessage : class
{
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task InvokeAsync(ProducerContext<TMessage> context, ProducerDelegate<TMessage> next)
    {
        await _lock.WaitAsync(context.CancellationToken);
        try
        {
            if (_channel == null || !_channel.IsOpen)
            {
                var connection = await connectionManager.GetConnectionAsync(broker);
                _channel = await connection.CreateChannelAsync(cancellationToken: context.CancellationToken);
            }

            await _channel.BasicPublishAsync(
                exchange: context.Exchange,
                routingKey: context.RoutingKey,
                body: context.Body ?? [],
                basicProperties: context.Properties ?? new BasicProperties(),
                mandatory: true,
                cancellationToken: context.CancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        await next(context);
    }
}
