using System.Reflection;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Carotte;

public sealed class ConsumerMediator(IServiceProvider serviceProvider)
{
    private Type? _consumerType;
    private readonly Dictionary<Type, MethodInfo> _handlerMethods = [];

    public void Initialize<TConsumer>() where TConsumer : class
    {
        _consumerType = typeof(TConsumer);
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

    public IEnumerable<Type> GetHandledMessageTypes() => _handlerMethods.Keys;

    public Type? ResolveMessageType(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Type != null && _handlerMethods.Keys.Any(k => k.Name == ea.BasicProperties.Type))
        {
            return _handlerMethods.Keys.FirstOrDefault(k => k.Name == ea.BasicProperties.Type);
        }

        return _handlerMethods.Count == 1
            ? _handlerMethods.Keys.First()
            : null;
    }

    public async Task InvokeAsync(Type messageType, object message, CancellationToken cancellationToken)
    {
        if (_consumerType != null && _handlerMethods.TryGetValue(messageType, out var method))
        {
            var handler = serviceProvider.GetRequiredService(_consumerType);
            var task = (Task?)method.Invoke(handler, [message, cancellationToken]);
            if (task != null) await task;
        }
    }
}
