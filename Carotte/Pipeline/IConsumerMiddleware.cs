namespace Carotte.Pipeline;

internal delegate Task ConsumerDelegate(ConsumerContext context);

internal interface IConsumerMiddleware
{
    Task InvokeAsync(ConsumerContext context, ConsumerDelegate next);
}
