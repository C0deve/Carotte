using System.Reflection;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte;

internal sealed class ConsumerMediator(
    IServiceProvider serviceProvider,
    IMessageTypeResolver? messageTypeResolver = null)
{
    private readonly IMessageTypeResolver _messageTypeResolver = messageTypeResolver ?? MessageTypeResolver.Default;
    private Type? _consumerType;
    private readonly Dictionary<Type, MethodInfo> _handlerMethods = [];

    public void Initialize(Type consumerType)
    {
        _consumerType = consumerType;
        var consumerInterfaces = _consumerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .ToList();

        foreach (var i in consumerInterfaces)
        {
            var messageType = i.GetGenericArguments()[0];
            var method = i.GetMethod(nameof(IConsumer<>.HandleAsync));
            if (method != null)
            {
                _handlerMethods[messageType] = method;
            }
        }
    }

    public void Initialize<TConsumer>() where TConsumer : class => Initialize(typeof(TConsumer));

    public IEnumerable<Type> GetHandledMessageTypes() => _handlerMethods.Keys;

    public Type? ResolveMessageType(BasicDeliverEventArgs ea)
    {
        return _messageTypeResolver.ResolveType(ea.BasicProperties.Type, _handlerMethods.Keys);
    }

    internal AsyncServiceScope CreateMessageScope() => serviceProvider.CreateAsyncScope();

    internal async Task InvokeAsync(
        IServiceProvider messageServiceProvider,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        if (_consumerType != null && _handlerMethods.TryGetValue(messageType, out var method))
        {
            var handler = messageServiceProvider.GetRequiredService(_consumerType);
            try
            {
                var task = (Task?)method.Invoke(handler, [message, cancellationToken]);
                if (task != null) await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}
