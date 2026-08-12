namespace Carotte.pipeline;

internal class ConsumerInvocationMiddleware(ConsumerMediator mediator) : IConsumerMiddleware
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        if (context is { Message: not null, MessageType: not null })
        {
            await mediator.InvokeAsync(context.Services, context.MessageType, context.Message, context.CancellationToken);
        }

        await next(context);
    }
}
