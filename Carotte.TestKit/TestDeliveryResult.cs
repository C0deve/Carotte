namespace Carotte;

public sealed record TestDeliveryResult
{
    public bool IsAcked { get; init; }
    public bool IsNacked { get; init; }
    public bool Requeued { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan ElapsedTime { get; init; }

    public static TestDeliveryResult Ack(TimeSpan elapsedTime) => new()
    {
        IsAcked = true,
        IsNacked = false,
        Requeued = false,
        Exception = null,
        ElapsedTime = elapsedTime
    };

    public static TestDeliveryResult Nack(Exception exception, TimeSpan elapsedTime, bool requeued) => new()
    {
        IsAcked = false,
        IsNacked = true,
        Requeued = requeued,
        Exception = exception,
        ElapsedTime = elapsedTime
    };
}
