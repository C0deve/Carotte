using Microsoft.Extensions.DependencyInjection;

namespace Carotte;

public class CarotteTestKit(IServiceProvider serviceProvider)
{
    public async Task SimulateReceiveAsync<TConsumer, TMessage>(TMessage message, CancellationToken cancellationToken = default) 
        where TConsumer : class, IConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(message);
        var mediator = serviceProvider.GetRequiredService<ConsumerMediator>();
        mediator.Initialize<TConsumer>();
        await mediator.InvokeAsync(typeof(TMessage), message, cancellationToken);
    }

    public IReadOnlyList<TMessage> GetSentMessages<TMessage>() where TMessage : class => 
        serviceProvider.GetRequiredService<MessageTestStore>().GetSentMessages<TMessage>();
}
