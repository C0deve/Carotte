namespace Carotte;

/// <summary>
/// Represents the result and execution metrics of simulating consumer message processing in Carotte TestKit.
/// </summary>
public sealed record TestDeliveryResult
{
    /// <summary>
    /// Gets a value indicating whether the message processing was acknowledged successfully (Ack).
    /// </summary>
    public bool IsAcked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message processing was rejected/failed (Nack).
    /// </summary>
    public bool IsNacked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message was marked to be requeued on failure.
    /// </summary>
    public bool Requeued { get; init; }

    /// <summary>
    /// Gets the exception thrown during message processing, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets the total execution duration of the message processing pipeline.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Creates a successful (Ack) delivery result.
    /// </summary>
    /// <param name="elapsedTime">The execution duration.</param>
    /// <returns>A successful <see cref="TestDeliveryResult"/> instance.</returns>
    public static TestDeliveryResult Ack(TimeSpan elapsedTime) => new()
    {
        IsAcked = true,
        IsNacked = false,
        Requeued = false,
        Exception = null,
        ElapsedTime = elapsedTime
    };

    /// <summary>
    /// Creates a failed (Nack) delivery result.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="elapsedTime">The execution duration up to failure.</param>
    /// <param name="requeued">Indicates whether the message was configured to be requeued on failure.</param>
    /// <returns>A failed <see cref="TestDeliveryResult"/> instance.</returns>
    public static TestDeliveryResult Nack(Exception exception, TimeSpan elapsedTime, bool requeued) => new()
    {
        IsAcked = false,
        IsNacked = true,
        Requeued = requeued,
        Exception = exception,
        ElapsedTime = elapsedTime
    };
}
