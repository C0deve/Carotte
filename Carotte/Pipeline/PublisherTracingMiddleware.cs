using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Carotte.pipeline;

internal class PublisherTracingMiddleware<TMessage> : IPublisherMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next)
    {
        using var activity =
            CarotteDiagnostics.ActivitySource.StartActivity($"Publish {context.RoutingKey}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", context.Exchange);
        activity?.SetTag("messaging.destination_kind", "exchange");
        activity?.SetTag("messaging.rabbitmq.routing_key", context.RoutingKey);

        try
        {
            context.Properties ??= new BasicProperties();
            context.Properties.Type = typeof(TMessage).Name;

            if (activity != null)
            {
                var propagationContext = new PropagationContext(activity.Context, Baggage.Current);
                CarotteDiagnostics.Propagator.Inject(propagationContext, context.Properties, (props, key, value) =>
                {
                    props.Headers ??= new Dictionary<string, object?>();
                    props.Headers[key] = value;
                });
            }

            await next(context);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
