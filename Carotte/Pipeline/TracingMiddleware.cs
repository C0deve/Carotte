using System.Diagnostics;
using System.Text;

namespace Carotte.pipeline;

internal class TracingMiddleware : IConsumerMiddleware
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        var ea = context.DeliveryArgs;
        var parentContext = CarotteDiagnostics.Propagator.Extract(default, ea.BasicProperties, (props, key) =>
        {
            if (props.Headers == null || !props.Headers.TryGetValue(key, out var value)) return [];
            if (value is byte[] bytes) return [Encoding.UTF8.GetString(bytes)];
            return [value?.ToString() ?? string.Empty];
        });

        using var activity = CarotteDiagnostics.ActivitySource.StartActivity(
            $"Consume {context.MessageType?.Name ?? "Unknown"}",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", ea.RoutingKey);
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
