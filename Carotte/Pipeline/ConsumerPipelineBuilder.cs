namespace Carotte.pipeline;

public class ConsumerPipelineBuilder
{
    private readonly List<IConsumerMiddleware> _middlewares = [];

    public ConsumerPipelineBuilder Use(IConsumerMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public ConsumerPipeline Build()
    {
        ConsumerDelegate next = _ => Task.CompletedTask;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return new ConsumerPipeline(next);
    }
}