using System.Collections.Concurrent;

namespace Carotte;

/// <summary>
/// Thread-safe in-memory storage for capturing and inspecting messages published during test execution.
/// </summary>
public class MessageTestStore
{
    private readonly ConcurrentQueue<object> _messages = new();

    /// <summary>
    /// Event raised whenever a new message is published and added to the store.
    /// </summary>
    public event Action<object>? MessageAdded;

    /// <summary>
    /// Gets a snapshot of all messages published and stored so far.
    /// </summary>
    public IReadOnlyList<object> PublishedMessages => _messages.ToList();

    /// <summary>
    /// Gets a snapshot of published messages filtered by the specified type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message payload type.</typeparam>
    /// <returns>A read-only list of matching published messages.</returns>
    public IReadOnlyList<T> GetPublishedMessages<T>() => _messages.OfType<T>().ToList();

    /// <summary>
    /// Adds a published message to the store and notifies all registered listeners.
    /// </summary>
    /// <param name="message">The message instance to record.</param>
    public void Add(object message)
    {
        _messages.Enqueue(message);
        MessageAdded?.Invoke(message);
    }

    /// <summary>
    /// Clears all recorded messages from the store.
    /// </summary>
    public void Clear() => _messages.Clear();
}
