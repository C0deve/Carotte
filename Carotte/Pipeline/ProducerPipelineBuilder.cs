namespace Carotte.pipeline;

public class ProducerPipelineBuilder<TMessage> where TMessage : class
{
    private readonly List<IProducerMiddleware<TMessage>> _middlewares = [];

    public ProducerPipelineBuilder<TMessage> Use(IProducerMiddleware<TMessage> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public ProducerPipeline<TMessage> Build()
    {
        ProducerDelegate<TMessage> next = _ => Task.CompletedTask;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return new ProducerPipeline<TMessage>(next);
    }
}