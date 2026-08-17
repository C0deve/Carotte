using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Carotte.pipeline;

namespace Carotte;

public class CarotteTestKit(IServiceProvider serviceProvider)
{
    public Task SimulateReceiveAsync<TConsumer, TMessage>(TMessage message, CancellationToken cancellationToken = default) 
        where TConsumer : class, IConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(message);
        return SimulateReceiveInternalAsync(typeof(TConsumer), typeof(TMessage), message, cancellationToken);
    }

    public Task SimulateReceiveAsync<TConsumer>(object message, CancellationToken cancellationToken = default) 
        where TConsumer : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.GetType();
        var consumerInterface = typeof(IConsumer<>).MakeGenericType(messageType);
        if (!consumerInterface.IsAssignableFrom(typeof(TConsumer)))
        {
            throw new InvalidOperationException($"Consumer '{typeof(TConsumer).FullName}' does not implement IConsumer<{messageType.FullName}>.");
        }

        return SimulateReceiveInternalAsync(typeof(TConsumer), messageType, message, cancellationToken);
    }

    public async Task SimulateReceiveAsync(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.GetType();
        var consumerTypes = FindConsumerTypes(messageType).ToList();

        if (consumerTypes.Count == 0)
        {
            throw new InvalidOperationException($"No consumer found implementing IConsumer<{messageType.FullName}>.");
        }

        foreach (var consumerType in consumerTypes)
        {
            await SimulateReceiveInternalAsync(consumerType, messageType, message, cancellationToken);
        }
    }

    private async Task SimulateReceiveInternalAsync(
        Type consumerType,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        var queueAttr = consumerType.GetCustomAttribute<QueueAttribute>();
        var routingKey = !string.IsNullOrEmpty(queueAttr?.RoutingKey)
            ? queueAttr.RoutingKey
            : (queueAttr?.Name ?? messageType.Name);

        var properties = new BasicProperties
        {
            Type = messageType.Name
        };

        var ea = new BasicDeliverEventArgs(
            consumerTag: "testkit-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: queueAttr?.Exchange ?? string.Empty,
            routingKey: routingKey,
            properties: properties,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();

        var mediator = scope.ServiceProvider.GetService<ConsumerMediator>() ?? new ConsumerMediator(scope.ServiceProvider);
        mediator.Initialize(consumerType);

        var serializer = scope.ServiceProvider.GetService<ISerializer>() ?? new JsonSerializerImpl();

        var pipeline = new ConsumerPipelineBuilder()
            .Use(new MetricsMiddleware())
            .Use(new TracingMiddleware())
            .Use(new DeserializationMiddleware(serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();

        var context = new ConsumerContext(ea, scope.ServiceProvider, Message: message, MessageType: messageType, CancellationToken: cancellationToken);

        var maxRetryAttempts = Math.Max(0, queueAttr?.MaxRetryAttempts ?? 3);
        var logger = scope.ServiceProvider.GetService<ILogger<CarotteTestKit>>();
        var attempt = 0;

        while (true)
        {
            try
            {
                await pipeline.ExecuteAsync(context);
                return;
            }
            catch (Exception ex) when (attempt < maxRetryAttempts)
            {
                attempt++;
                logger?.LogWarning(
                    ex,
                    "Message processing failed for consumer {ConsumerType}. Retrying attempt {RetryAttempt}/{MaxRetryAttempts}.",
                    consumerType.Name,
                    attempt,
                    maxRetryAttempts);
            }
        }
    }

    private IEnumerable<Type> FindConsumerTypes(Type messageType)
    {
        var targetInterface = typeof(IConsumer<>).MakeGenericType(messageType);

        var builder = serviceProvider.GetService<CarotteBuilder>();
        var assemblies = new HashSet<Assembly>();
        if (builder != null && builder.Assemblies.Count > 0)
        {
            assemblies.UnionWith(builder.Assemblies);
        }

        var namespaces = builder?.Namespaces ?? [];
        var consumerTypes = SearchAssemblies(assemblies, namespaces, targetInterface);

        if (consumerTypes.Count == 0)
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
            consumerTypes = SearchAssemblies(allAssemblies, namespaces, targetInterface);
        }

        return consumerTypes.Distinct();
    }

    private static List<Type> SearchAssemblies(IEnumerable<Assembly> assemblies, HashSet<string> namespaces, Type targetInterface)
    {
        var result = new List<Type>();
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type is { IsAbstract: false, IsClass: true } &&
                    (namespaces.Count == 0 || (type.Namespace != null && namespaces.Any(ns => type.Namespace == ns || type.Namespace.StartsWith(ns + ".")))) &&
                    targetInterface.IsAssignableFrom(type))
                {
                    result.Add(type);
                }
            }
        }
        return result;
    }

    public IReadOnlyList<TMessage> GetSentMessages<TMessage>() where TMessage : class => 
        serviceProvider.GetRequiredService<MessageTestStore>().GetSentMessages<TMessage>();
}
