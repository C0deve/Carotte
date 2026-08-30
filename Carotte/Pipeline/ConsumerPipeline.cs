namespace Carotte.Pipeline;

internal class ConsumerPipeline(ConsumerDelegate pipeline)
{
    public Task ExecuteAsync(ConsumerContext context) => pipeline(context);
}
