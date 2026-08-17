namespace Carotte;

public class InMemoryPublisher<TMessage>(MessageTestStore store) : IPublisher<TMessage> where TMessage : class
{
    public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        store.Add(message);
        return Task.CompletedTask;
    }
}
