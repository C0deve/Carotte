namespace Carotte.pipeline;

/// <summary>
/// Builds an execution pipeline for publishing outgoing messages by chaining <see cref="IPublisherMiddleware{TMessage}"/> instances.
/// </summary>
/// <typeparam name="TMessage">The type of the message being published.</typeparam>
internal class PublisherPipelineBuilder<TMessage>
{
    private readonly List<IPublisherMiddleware<TMessage>> _middlewares = [];

    /// <summary>
    /// Adds a middleware to the publication pipeline.
    /// Middlewares execute in the order they are added.
    /// </summary>
    public PublisherPipelineBuilder<TMessage> Use(IPublisherMiddleware<TMessage> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Builds the composed <see cref="PublisherPipeline{TMessage}"/> delegate chain (Russian-doll pattern).
    /// </summary>
    public PublisherPipeline<TMessage> Build()
    {
        PublisherDelegate<TMessage> next = _ => Task.CompletedTask;

        // Compose middlewares in reverse order so first added executes first
        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return new PublisherPipeline<TMessage>(next);
    }
}
