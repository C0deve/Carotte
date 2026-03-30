namespace Carotte;

public interface IConsumer<in TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}
