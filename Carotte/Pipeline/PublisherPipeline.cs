namespace Carotte.pipeline;

internal class PublisherPipeline<TMessage>(PublisherDelegate<TMessage> pipeline)
{
    public Task ExecuteAsync(PublisherContext<TMessage> context) => pipeline(context);
}
