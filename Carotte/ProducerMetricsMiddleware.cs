namespace Carotte;

public class ProducerMetricsMiddleware<TMessage> : IProducerMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(ProducerContext<TMessage> context, ProducerDelegate<TMessage> next)
    {
        await next(context);
        CarotteDiagnostics.MessagesProducedCounter.Add(1, new KeyValuePair<string, object?>("exchange", context.Exchange));
    }
}
