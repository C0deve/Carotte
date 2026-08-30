namespace Carotte.Pipeline;

/// <summary>
/// Builds an execution pipeline for incoming consumer messages by chaining <see cref="IConsumerMiddleware"/> instances.
/// </summary>
internal class ConsumerPipelineBuilder
{
    private readonly List<IConsumerMiddleware> _middlewares = [];

    /// <summary>
    /// Adds a middleware to the consumer pipeline.
    /// Middlewares execute in the order they are added.
    /// </summary>
    public ConsumerPipelineBuilder Use(IConsumerMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Builds the composed <see cref="ConsumerPipeline"/> delegate chain (Russian-doll pattern).
    /// </summary>
    public ConsumerPipeline Build()
    {
        ConsumerDelegate next = _ => Task.CompletedTask;

        // Compose middlewares in reverse order so first added executes first
        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return new ConsumerPipeline(next);
    }
}
