using System.Collections.Concurrent;

namespace Carotte;

public class MessageTestStore
{
    private readonly ConcurrentQueue<object> _messages = new();

    public IReadOnlyList<object> SentMessages => _messages.ToList();

    public IReadOnlyList<T> GetSentMessages<T>() where T : class => _messages.OfType<T>().ToList();

    public void Add(object message) => _messages.Enqueue(message);

    public void Clear() => _messages.Clear();
}
