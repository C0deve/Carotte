namespace Carotte.pipeline;

internal class PublisherPipeline<TMessage>(PublisherDelegate<TMessage> pipeline) where TMessage : class
{
    public Task ExecuteAsync(PublisherContext<TMessage> context) => pipeline(context);
}
