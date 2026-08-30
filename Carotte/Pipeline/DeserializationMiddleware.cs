using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Carotte.Pipeline;

internal class DeserializationMiddleware(ISerializer serializer) : IConsumerMiddleware
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        if (context is { MessageType: not null, Message: null })
        {
            var body = context.DeliveryArgs.Body.ToArray();
            var deserializeMethod = serializer.GetType().GetMethod(nameof(ISerializer.Deserialize))?.MakeGenericMethod(context.MessageType);
            object? message = null;
            try
            {
                message = deserializeMethod?.Invoke(serializer, [body]);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            await next(context with { Message = message });
        }
        else
            await next(context);
    }
}
