namespace Carotte.pipeline;

internal class SerializationMiddleware<TMessage>(ISerializer serializer) : IPublisherMiddleware<TMessage>
{
    public async Task InvokeAsync(PublisherContext<TMessage> context, PublisherDelegate<TMessage> next)
    {
        context.Body ??= serializer.Serialize(context.Message);

        await next(context);
    }
}
