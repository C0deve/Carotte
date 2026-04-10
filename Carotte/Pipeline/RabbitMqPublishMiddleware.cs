using RabbitMQ.Client;

namespace Carotte.pipeline;

public class RabbitMqPublishMiddleware<TMessage>(IRabbitMqClient rabbitMqClient, string broker) : IProducerMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(ProducerContext<TMessage> context, ProducerDelegate<TMessage> next)
    {
        await rabbitMqClient.BasicPublishAsync<TMessage>(
            broker: broker,
            exchange: context.Exchange,
            routingKey: context.RoutingKey,
            body: context.Body ?? [],
            properties: context.Properties ?? new BasicProperties(),
            mandatory: true,
            cancellationToken: context.CancellationToken);

        await next(context);
    }
}
