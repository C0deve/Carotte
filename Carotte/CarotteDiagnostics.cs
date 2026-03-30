using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carotte;

public static class CarotteDiagnostics
{
    public const string ServiceName = "Carotte";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> MessagesConsumedCounter = Meter.CreateCounter<long>("carotte_messages_consumed", description: "Number of messages consumed");
    public static readonly Counter<long> MessagesProducedCounter = Meter.CreateCounter<long>("carotte_messages_produced", description: "Number of messages produced");
    public static readonly Counter<long> MessageErrorsCounter = Meter.CreateCounter<long>("carotte_message_errors", description: "Number of message processing errors");
    
    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>("carotte_message_processing_duration", unit: "ms", description: "Processing duration of consumed messages");
}
