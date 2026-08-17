using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Carotte.pipeline;

namespace Carotte;

public class CarotteTestKit(IServiceProvider serviceProvider)
{
    public async Task SimulateReceiveAsync<TConsumer, TMessage>(TMessage message, CancellationToken cancellationToken = default) 
        where TConsumer : class, IConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(message);

        var queueAttr = typeof(TConsumer).GetCustomAttribute<QueueAttribute>();
        var routingKey = !string.IsNullOrEmpty(queueAttr?.RoutingKey)
            ? queueAttr.RoutingKey
            : (queueAttr?.Name ?? typeof(TMessage).Name);

        var properties = new BasicProperties
        {
            Type = typeof(TMessage).Name
        };

        var ea = new BasicDeliverEventArgs(
            consumerTag: "testkit-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: queueAttr?.Exchange ?? string.Empty,
            routingKey: routingKey,
            properties: properties,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();

        var mediator = scope.ServiceProvider.GetService<ConsumerMediator>() ?? new ConsumerMediator(scope.ServiceProvider);
        mediator.Initialize<TConsumer>();

        var serializer = scope.ServiceProvider.GetService<ISerializer>() ?? new JsonSerializerImpl();

        var pipeline = new ConsumerPipelineBuilder()
            .Use(new MetricsMiddleware())
            .Use(new TracingMiddleware())
            .Use(new DeserializationMiddleware(serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();

        var context = new ConsumerContext(ea, scope.ServiceProvider, Message: message, MessageType: typeof(TMessage), CancellationToken: cancellationToken);

        var maxRetryAttempts = Math.Max(0, queueAttr?.MaxRetryAttempts ?? 3);
        var logger = scope.ServiceProvider.GetService<ILogger<CarotteTestKit>>();
        var attempt = 0;

        while (true)
        {
            try
            {
                await pipeline.ExecuteAsync(context);
                return;
            }
            catch (Exception ex) when (attempt < maxRetryAttempts)
            {
                attempt++;
                logger?.LogWarning(
                    ex,
                    "Message processing failed for consumer {ConsumerType}. Retrying attempt {RetryAttempt}/{MaxRetryAttempts}.",
                    typeof(TConsumer).Name,
                    attempt,
                    maxRetryAttempts);
            }
        }
    }

    public IReadOnlyList<TMessage> GetSentMessages<TMessage>() where TMessage : class => 
        serviceProvider.GetRequiredService<MessageTestStore>().GetSentMessages<TMessage>();
}
