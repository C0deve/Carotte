namespace Carotte;

/// <summary>
/// In-memory implementation of <see cref="IPublisher{TMessage}"/> that records published messages
/// into a <see cref="MessageTestStore"/> for test assertions without interacting with a live broker.
/// </summary>
/// <typeparam name="TMessage">The message payload type.</typeparam>
/// <param name="store">The message store used to record published messages.</param>
public class InMemoryPublisher<TMessage>(MessageTestStore store) : IPublisher<TMessage>
{
    /// <summary>
    /// Publishes a message by storing it in the in-memory <see cref="MessageTestStore"/>.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        store.Add(message!);
        return Task.CompletedTask;
    }
}
