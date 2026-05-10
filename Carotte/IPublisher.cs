namespace Carotte;

public interface IPublisher<in TMessage> where TMessage : class
{
    Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);
}
