using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Carotte.Pipeline;

// ReSharper disable once CheckNamespace
namespace Carotte;

public class CarotteTestKit(IServiceProvider serviceProvider)
{
    public Task<TestDeliveryResult> SimulateReceiveAsync<TConsumer, TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(message);
        return SimulateReceiveInternalAsync(typeof(TConsumer), typeof(TMessage), message, cancellationToken);
    }

    public Task<TestDeliveryResult> SimulateReceiveAsync<TConsumer>(object message, CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<TestDeliveryResult>> SimulateReceiveAsync(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.GetType();
        var consumerTypes = FindConsumerTypes(messageType).ToList();

        if (consumerTypes.Count == 0)
        {
            throw new InvalidOperationException($"No consumer found implementing IConsumer<{messageType.FullName}>.");
        }

        var results = new List<TestDeliveryResult>();
        foreach (var consumerType in consumerTypes)
        {
            var result = await SimulateReceiveInternalAsync(consumerType, messageType, message, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<TestDeliveryResult> SimulateReceiveInternalAsync(
        Type consumerType,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        var queueAttr = consumerType.GetCustomAttribute<QueueAttribute>();

        var options = serviceProvider.GetService<IOptions<CarotteOptions>>()?.Value;
        var builder = serviceProvider.GetService<CarotteBuilder>();
        var consumerSettingsDict = options?.Consumers ?? builder?.ConsumerSettings;

        ConsumerSettingsOptions? overrideSettings = null;
        if (consumerSettingsDict != null)
        {
            if (consumerSettingsDict.TryGetValue(consumerType.Name, out var byName))
                overrideSettings = byName;
            else if (consumerType.FullName != null && consumerSettingsDict.TryGetValue(consumerType.FullName, out var byFullName))
                overrideSettings = byFullName;
            else if (queueAttr?.Name != null && consumerSettingsDict.TryGetValue(queueAttr.Name, out var byQueue))
                overrideSettings = byQueue;
        }

        var routingKey = overrideSettings?.RoutingKey
            ?? (!string.IsNullOrEmpty(queueAttr?.RoutingKey)
                ? queueAttr.RoutingKey
                : (overrideSettings?.QueueName ?? queueAttr?.Name ?? messageType.Name));

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

        ConsumerPipeline pipeline;
        var existingPipeline = scope.ServiceProvider.GetService<ConsumerPipeline>();
        if (existingPipeline != null)
        {
            pipeline = existingPipeline;
        }
        else
        {
            var pipelineBuilder = scope.ServiceProvider.GetService<ConsumerPipelineBuilder>() ?? new ConsumerPipelineBuilder();
            var registeredMiddlewares = scope.ServiceProvider.GetServices<IConsumerMiddleware>().ToList();
            if (registeredMiddlewares.Count > 0)
            {
                foreach (var middleware in registeredMiddlewares)
                {
                    pipelineBuilder.Use(middleware);
                }
            }
            else
            {
                pipelineBuilder
                    .Use(scope.ServiceProvider.GetService<MetricsMiddleware>() ?? new MetricsMiddleware())
                    .Use(scope.ServiceProvider.GetService<TracingMiddleware>() ?? new TracingMiddleware())
                    .Use(scope.ServiceProvider.GetService<DeserializationMiddleware>() ?? new DeserializationMiddleware(serializer));
            }

            pipelineBuilder.Use(new ConsumerInvocationMiddleware(mediator));
            pipeline = pipelineBuilder.Build();
        }

        var context = new ConsumerContext(ea, scope.ServiceProvider, Message: message, MessageType: messageType, CancellationToken: cancellationToken);

        var maxRetryAttempts = Math.Max(0, overrideSettings?.MaxRetryAttempts ?? queueAttr?.MaxRetryAttempts ?? 3);
        var failureAction = overrideSettings?.FailureAction ?? queueAttr?.FailureAction ?? ConsumerFailureAction.DeadLetter;
        var requeueOnFailure = failureAction == ConsumerFailureAction.Requeue;
        var logger = scope.ServiceProvider.GetService<ILogger<CarotteTestKit>>();
        var attempt = 0;

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                await pipeline.ExecuteAsync(context);
                stopwatch.Stop();
                return TestDeliveryResult.Ack(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                if (attempt < maxRetryAttempts)
                {
                    attempt++;
                    logger?.LogWarning(
                        ex,
                        "Message processing failed for consumer {ConsumerType}. Retrying attempt {RetryAttempt}/{MaxRetryAttempts}.",
                        consumerType.Name,
                        attempt,
                        maxRetryAttempts);
                }
                else
                {
                    stopwatch.Stop();
                    logger?.LogError(
                        ex,
                        "Message processing failed for consumer {ConsumerType}. Nacking with requeue={Requeue}.",
                        consumerType.Name,
                        requeueOnFailure);

                    return TestDeliveryResult.Nack(ex, stopwatch.Elapsed, requeueOnFailure);
                }
            }
        }
    }

    private IEnumerable<Type> FindConsumerTypes(Type messageType)
    {
        var targetInterface = typeof(IConsumer<>).MakeGenericType(messageType);

        var builder = serviceProvider.GetService<CarotteBuilder>();
        var assemblies = new HashSet<Assembly>();
        if (builder is { Assemblies.Count: > 0 })
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

    private static List<Type> SearchAssemblies(IEnumerable<Assembly> assemblies, HashSet<string> namespaces, Type targetInterface) =>
    [
        .. assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => IsCandidateConsumer(type, namespaces, targetInterface))
    ];

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static bool IsCandidateConsumer(Type type, HashSet<string> namespaces, Type targetInterface) =>
        type is { IsAbstract: false, IsClass: true } &&
        IsInNamespace(type, namespaces) &&
        targetInterface.IsAssignableFrom(type);

    private static bool IsInNamespace(Type type, HashSet<string> namespaces) =>
        namespaces.Count == 0 || (type.Namespace != null && namespaces.Any(ns => type.Namespace == ns || type.Namespace.StartsWith(ns + ".")));

    public IReadOnlyList<TMessage> GetSentMessages<TMessage>() =>
        serviceProvider.GetRequiredService<MessageTestStore>().GetSentMessages<TMessage>();

    public void Clear() =>
        serviceProvider.GetRequiredService<MessageTestStore>().Clear();

    public TMessage ShouldHavePublished<TMessage>(Func<TMessage, bool>? predicate = null)
    {
        var messages = GetSentMessages<TMessage>();

        if (predicate == null)
        {
            if (messages.Count == 0)
            {
                throw new CarotteTestAssertionException(
                    $"Expected at least one message of type '{typeof(TMessage).Name}' to be published, but none was found.");
            }
            return messages[0];
        }

        var matching = messages.FirstOrDefault(predicate);
        return matching ??
               throw new CarotteTestAssertionException(
                   $"Expected a message of type '{typeof(TMessage).Name}' matching the predicate to be published, but none was found.");
    }

    public void ShouldNotHavePublished<TMessage>(Func<TMessage, bool>? predicate = null)
    {
        var messages = GetSentMessages<TMessage>();

        if (predicate == null)
        {
            if (messages.Count > 0)
                throw new CarotteTestAssertionException(
                    $"Expected no message of type '{typeof(TMessage).Name}' to be published, but found {messages.Count}.");
        }
        else
        {
            var matchingCount = messages.Count(predicate);
            if (matchingCount > 0)
                throw new CarotteTestAssertionException(
                    $"Expected no message of type '{typeof(TMessage).Name}' matching the predicate to be published, but found {matchingCount}.");
        }
    }

    public async Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        Func<TMessage, bool>? predicate = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var store = serviceProvider.GetRequiredService<MessageTestStore>();
        predicate ??= _ => true;

        var existing = store.GetSentMessages<TMessage>().FirstOrDefault(predicate);
        if (existing != null)
            return existing;

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        var tcs = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object msg)
        {
            if (msg is TMessage typedMsg && predicate(typedMsg))
                tcs.TrySetResult(typedMsg);
        }

        store.MessageAdded += Handler;
        try
        {
            var doubleCheck = store.GetSentMessages<TMessage>().FirstOrDefault(predicate);
            if (doubleCheck != null)
                return doubleCheck;

            await using (cts.Token.Register(() =>
                         {
                             if (cancellationToken.IsCancellationRequested)
                                 tcs.TrySetCanceled(cancellationToken);
                             else
                                 tcs.TrySetException(new TimeoutException(
                                     $"Timed out after {effectiveTimeout.TotalMilliseconds}ms waiting for message of type '{typeof(TMessage).Name}'."));
                         }))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            store.MessageAdded -= Handler;
        }
    }

    public Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForPublishedMessageAsync<TMessage>(predicate: null, timeout: (TimeSpan?)timeout, cancellationToken: cancellationToken);

    public Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        Func<TMessage, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForPublishedMessageAsync(predicate, (TimeSpan?)timeout, cancellationToken);
}
