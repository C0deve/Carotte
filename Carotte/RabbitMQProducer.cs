using Carotte.pipeline;
namespace Carotte;

public class RabbitMqProducer<TMessage>(IRabbitMqClient rabbitMqClient, ISerializer serializer, string broker, string exchange)
    : IProducer<TMessage>
    where TMessage : class
{
    private readonly ProducerPipeline<TMessage> _pipeline = new ProducerPipelineBuilder<TMessage>()
        .Use(new ProducerMetricsMiddleware<TMessage>())
        .Use(new ProducerTracingMiddleware<TMessage>())
        .Use(new SerializationMiddleware<TMessage>(serializer))
        .Use(new RabbitMqPublishMiddleware<TMessage>(rabbitMqClient, broker))
        .Build();

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var routingKey = typeof(TMessage).Name;
        var context = new ProducerContext<TMessage>(message, exchange, routingKey, cancellationToken);
        await _pipeline.ExecuteAsync(context);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
