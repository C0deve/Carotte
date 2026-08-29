using System.Diagnostics;

namespace Carotte.pipeline;

internal class MetricsMiddleware : IConsumerMiddleware
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        var ea = context.DeliveryArgs;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
            CarotteDiagnostics.MessagesConsumedCounter.Add(
                1,
                new KeyValuePair<string, object?>("queue", ea.RoutingKey));
        }
        catch (Exception)
        {
            CarotteDiagnostics.MessageErrorsCounter.Add(
                1,
                new KeyValuePair<string, object?>("queue", ea.RoutingKey));
            throw;
        }
        finally
        {
            stopwatch.Stop();
            CarotteDiagnostics.MessageProcessingDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("queue", ea.RoutingKey));
        }
    }
}
