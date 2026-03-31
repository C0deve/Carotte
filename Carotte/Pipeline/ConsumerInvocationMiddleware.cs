using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte.pipeline;

public class ConsumerInvocationMiddleware<TConsumer>(IServiceProvider serviceProvider, Dictionary<Type, MethodInfo> handlerMethods) : IConsumerMiddleware
    where TConsumer : class
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        if (context is { Message: not null, MessageType: not null })
        {
            var handler = serviceProvider.GetRequiredService<TConsumer>();
            var task = (Task?)handlerMethods[context.MessageType].Invoke(handler, [context.Message, context.CancellationToken]);
            if (task != null) await task;
        }

        await next(context);
    }
}
