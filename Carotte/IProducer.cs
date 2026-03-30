namespace Carotte;

public interface IProducer<in TMessage>
{
    Task SendAsync(TMessage message, CancellationToken cancellationToken = default);
}
