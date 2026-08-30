namespace Carotte;

public class InMemoryPublisher<TMessage>(MessageTestStore store) : IPublisher<TMessage>
{
    public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        store.Add(message!);
        return Task.CompletedTask;
    }
}
