namespace Carotte;

public interface IProducer<in TMessage> where TMessage : class
{
    Task SendAsync(TMessage message, CancellationToken cancellationToken = default);
}
