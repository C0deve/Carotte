namespace Carotte.pipeline;

public class ProducerPipeline<TMessage>(ProducerDelegate<TMessage> pipeline) where TMessage : class
{
    public Task ExecuteAsync(ProducerContext<TMessage> context) => pipeline(context);
}