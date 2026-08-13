using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Context.Propagation;

namespace Carotte;

internal static class CarotteDiagnostics
{
    public const string ServiceName = "Carotte";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    private static readonly Meter Meter = new(ServiceName);
    public static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static readonly Counter<long> MessagesConsumedCounter = Meter.CreateCounter<long>(
        "carotte_messages_consumed",
        description: "Number of messages consumed");
    public static readonly Counter<long> MessagesPublishedCounter = Meter.CreateCounter<long>(
        "carotte_messages_published",
        description: "Number of messages published");
    public static readonly Counter<long> MessageErrorsCounter = Meter.CreateCounter<long>("carotte_message_errors",
        description: "Number of message processing errors");
    
    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        "carotte_message_processing_duration",
        unit: "ms",
        description: "Processing duration of consumed messages");
}
