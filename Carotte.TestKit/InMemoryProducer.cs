namespace Carotte;

internal class InMemoryProducer<TMessage>(MessageTestStore store) : IProducer<TMessage> where TMessage : class
{
    public Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        store.Add(message);
        return Task.CompletedTask;
    }
}
