namespace Carotte.pipeline;

public class SerializationMiddleware<TMessage>(ISerializer serializer) : IPublisherMiddleware<TMessage> where TMessage : class
{
    public async Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next)
    {
        context.Body ??= serializer.Serialize(context.Message);

        await next(context);
    }
}
