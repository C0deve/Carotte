namespace Carotte;

public class DeserializationMiddleware(ISerializer serializer) : IConsumerMiddleware
{
    public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
    {
        if (context is { MessageType: not null, Message: null })
        {
            var body = context.DeliveryArgs.Body.ToArray();
            var deserializeMethod = serializer.GetType().GetMethod(nameof(ISerializer.Deserialize))?.MakeGenericMethod(context.MessageType);
            var message = deserializeMethod?.Invoke(serializer, [body]);

            await next(context with { Message = message });
        }
        else
            await next(context);
    }
}