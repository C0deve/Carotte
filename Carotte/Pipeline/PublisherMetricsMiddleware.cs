namespace Carotte.Pipeline;

internal class PublisherMetricsMiddleware<TMessage> : IPublisherMiddleware<TMessage>
{
    public async Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next)
    {
        await next(context);
        CarotteDiagnostics.MessagesPublishedCounter.Add(
            1,
            new KeyValuePair<string, object?>("exchange", context.Exchange));
    }
}
