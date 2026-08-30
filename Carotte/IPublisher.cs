namespace Carotte;

public interface IPublisher<in TMessage>
{
    Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);
}
