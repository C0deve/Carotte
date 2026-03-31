namespace Carotte.pipeline;

public class SerializationMiddleware<TMessage>(ISerializer serializer) : IProducerMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(ProducerContext<TMessage> context, ProducerDelegate<TMessage> next)
    {
        context.Body ??= serializer.Serialize(context.Message);

        await next(context);
    }
}
