using RabbitMQ.Client;

namespace Carotte.pipeline;

public class RabbitMqPublishMiddleware<TMessage>(IRabbitMqClient rabbitMqClient, string broker) : IPublisherMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next)
    {
        await rabbitMqClient.BasicPublishAsync<TMessage>(
            exchange: context.Exchange,
            routingKey: context.RoutingKey,
            body: context.Body ?? [],
            properties: context.Properties ?? new BasicProperties(),
            mandatory: true,
            cancellationToken: context.CancellationToken);

        await next(context);
    }
}
