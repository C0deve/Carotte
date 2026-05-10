namespace Carotte.pipeline;

public class PublisherPipelineBuilder<TMessage> where TMessage : class
{
    private readonly List<IPublisherMiddleware<TMessage>> _middlewares = [];

    public PublisherPipelineBuilder<TMessage> Use(IPublisherMiddleware<TMessage> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public PublisherPipeline<TMessage> Build()
    {
        PublisherDelegate<TMessage> next = _ => Task.CompletedTask;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return new PublisherPipeline<TMessage>(next);
    }
}
