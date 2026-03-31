namespace Carotte.pipeline;

public class ConsumerPipeline(ConsumerDelegate pipeline)
{
    public Task ExecuteAsync(ConsumerContext context) => pipeline(context);
}