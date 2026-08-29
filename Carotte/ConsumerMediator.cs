using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;

namespace Carotte;

internal interface IMessageInvoker
{
    Task InvokeAsync(object consumer, object message, CancellationToken cancellationToken);
}

internal sealed class MessageInvoker<TMessage> : IMessageInvoker
{
    public Task InvokeAsync(object consumer, object message, CancellationToken cancellationToken) => 
        ((IConsumer<TMessage>)consumer).HandleAsync((TMessage)message, cancellationToken);
}

internal sealed class ConsumerMediator(
    IServiceProvider serviceProvider,
    IMessageTypeResolver? messageTypeResolver = null)
{
    private readonly IMessageTypeResolver _messageTypeResolver = messageTypeResolver ?? MessageTypeResolver.Default;
    private Type? _consumerType;
    private readonly Dictionary<Type, IMessageInvoker> _invokers = [];
    private IMessageInvoker? _singleInvoker;
    private Type? _singleMessageType;

    public void Initialize(Type consumerType)
    {
        _consumerType = consumerType;
        _invokers.Clear();
        _singleInvoker = null;
        _singleMessageType = null;

        var consumerInterfaces = _consumerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>));

        foreach (var i in consumerInterfaces)
        {
            var messageType = i.GetGenericArguments()[0];
            var invokerType = typeof(MessageInvoker<>).MakeGenericType(messageType);
            var invoker = (IMessageInvoker)Activator.CreateInstance(invokerType)!;
            _invokers[messageType] = invoker;
        }

        if (_invokers.Count != 1) 
            return;
        
        var singlePair = _invokers.First();
        _singleMessageType = singlePair.Key;
        _singleInvoker = singlePair.Value;
    }

    public void Initialize<TConsumer>() where TConsumer : class => Initialize(typeof(TConsumer));

    public IEnumerable<Type> GetHandledMessageTypes() => _invokers.Keys;

    public Type? ResolveMessageType(BasicDeliverEventArgs ea) => 
        _messageTypeResolver.ResolveType(ea.BasicProperties.Type, _invokers.Keys);

    internal AsyncServiceScope CreateMessageScope() => serviceProvider.CreateAsyncScope();

    internal Task InvokeAsync(
        IServiceProvider messageServiceProvider,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        if (_consumerType is null)
            return Task.CompletedTask;

        var invoker = _singleInvoker is not null && _singleMessageType == messageType
            ? _singleInvoker
            : _invokers.GetValueOrDefault(messageType);

        if (invoker is null)
            return Task.CompletedTask;

        var handler = messageServiceProvider.GetRequiredService(_consumerType);
        return invoker.InvokeAsync(handler, message, cancellationToken);
    }
}
