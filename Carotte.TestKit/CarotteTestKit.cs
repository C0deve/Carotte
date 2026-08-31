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

/// <summary>
/// Testing harness for Carotte messaging components, providing in-memory consumer execution
/// and published message assertions without requiring a live RabbitMQ broker.
/// </summary>
/// <param name="serviceProvider">The application's service provider used to resolve dependencies, consumers, and pipelines.</param>
public class CarotteTestKit(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Simulates receiving a message and executes the specified consumer through the full middleware pipeline.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type that implements <see cref="IConsumer{TMessage}"/>.</typeparam>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="message">The message instance to deliver to the consumer.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="TestDeliveryResult"/> containing acknowledgment status, elapsed time, and any error details.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
    public Task<TestDeliveryResult> ConsumeAsync<TConsumer, TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(message);
        return ConsumeInternalAsync(typeof(TConsumer), typeof(TMessage), message, cancellationToken);
    }

    /// <summary>
    /// Simulates receiving an untyped message and executes the specified consumer type through the middleware pipeline.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type.</typeparam>
    /// <param name="message">The message instance to deliver.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="TestDeliveryResult"/> containing the delivery outcome.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TConsumer"/> does not implement <see cref="IConsumer{TMessage}"/> for the message's runtime type.</exception>
    public Task<TestDeliveryResult> ConsumeAsync<TConsumer>(object message, CancellationToken cancellationToken = default)
        where TConsumer : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.GetType();
        var consumerInterface = typeof(IConsumer<>).MakeGenericType(messageType);
        if (!consumerInterface.IsAssignableFrom(typeof(TConsumer)))
        {
            throw new InvalidOperationException($"Consumer '{typeof(TConsumer).FullName}' does not implement IConsumer<{messageType.FullName}>.");
        }

        return ConsumeInternalAsync(typeof(TConsumer), messageType, message, cancellationToken);
    }

    /// <summary>
    /// Dispatches a message to all registered consumers in the application matching the message type.
    /// </summary>
    /// <param name="message">The message instance to broadcast to all matching consumers.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of <see cref="TestDeliveryResult"/> for each invoked consumer.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no consumer is registered for the message type.</exception>
    public async Task<IReadOnlyList<TestDeliveryResult>> ConsumeAsync(object message, CancellationToken cancellationToken = default)
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
            var result = await ConsumeInternalAsync(consumerType, messageType, message, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<TestDeliveryResult> ConsumeInternalAsync(
        Type consumerType,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        var queueAttr = consumerType.GetCustomAttribute<QueueAttribute>();
        var overrideSettings = ResolveConsumerSettings(consumerType, queueAttr);
        var ea = CreateDeliveryEventArgs(messageType, queueAttr, overrideSettings, cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();
        var pipeline = ResolvePipeline(scope.ServiceProvider, consumerType);
        var context = new ConsumerContext(ea, scope.ServiceProvider, Message: message, MessageType: messageType, CancellationToken: cancellationToken);

        var maxRetryAttempts = Math.Max(0, overrideSettings?.MaxRetryAttempts ?? queueAttr?.MaxRetryAttempts ?? 3);
        var failureAction = overrideSettings?.FailureAction ?? queueAttr?.FailureAction ?? ConsumerFailureAction.DeadLetter;
        var requeueOnFailure = failureAction == ConsumerFailureAction.Requeue;
        var logger = scope.ServiceProvider.GetService<ILogger<CarotteTestKit>>();

        return await ExecuteWithRetryAsync(pipeline, context, consumerType, maxRetryAttempts, requeueOnFailure, logger);
    }

    private ConsumerSettingsOptions? ResolveConsumerSettings(Type consumerType, QueueAttribute? queueAttr)
    {
        var options = serviceProvider.GetService<IOptions<CarotteOptions>>()?.Value;
        var builder = serviceProvider.GetService<CarotteBuilder>();
        var consumerSettingsDict = options?.Consumers ?? builder?.ConsumerSettings;

        if (consumerSettingsDict == null)
            return null;

        if (consumerSettingsDict.TryGetValue(consumerType.Name, out var byName))
            return byName;

        if (consumerType.FullName != null && consumerSettingsDict.TryGetValue(consumerType.FullName, out var byFullName))
            return byFullName;

        if (queueAttr?.Name != null && consumerSettingsDict.TryGetValue(queueAttr.Name, out var byQueue))
            return byQueue;

        return null;
    }

    private static BasicDeliverEventArgs CreateDeliveryEventArgs(
        Type messageType,
        QueueAttribute? queueAttr,
        ConsumerSettingsOptions? overrideSettings,
        CancellationToken cancellationToken)
    {
        var routingKey = overrideSettings?.RoutingKey
            ?? (!string.IsNullOrEmpty(queueAttr?.RoutingKey)
                ? queueAttr.RoutingKey
                : (overrideSettings?.QueueName ?? queueAttr?.Name ?? messageType.Name));

        var properties = new BasicProperties
        {
            Type = messageType.Name
        };

        return new BasicDeliverEventArgs(
            consumerTag: "testkit-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: queueAttr?.Exchange ?? string.Empty,
            routingKey: routingKey,
            properties: properties,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: cancellationToken);
    }

    private static ConsumerPipeline ResolvePipeline(IServiceProvider scopedProvider, Type consumerType)
    {
        var mediator = scopedProvider.GetService<ConsumerMediator>() ?? new ConsumerMediator(scopedProvider);
        mediator.Initialize(consumerType);

        var existingPipeline = scopedProvider.GetService<ConsumerPipeline>();
        if (existingPipeline != null)
        {
            return existingPipeline;
        }

        var pipelineBuilder = scopedProvider.GetService<ConsumerPipelineBuilder>() ?? new ConsumerPipelineBuilder();
        var registeredMiddlewares = scopedProvider.GetServices<IConsumerMiddleware>().ToList();
        if (registeredMiddlewares.Count > 0)
        {
            foreach (var middleware in registeredMiddlewares)
            {
                pipelineBuilder.Use(middleware);
            }
        }
        else
        {
            var serializer = scopedProvider.GetService<ISerializer>() ?? new JsonSerializerImpl();
            pipelineBuilder
                .Use(scopedProvider.GetService<MetricsMiddleware>() ?? new MetricsMiddleware())
                .Use(scopedProvider.GetService<TracingMiddleware>() ?? new TracingMiddleware())
                .Use(scopedProvider.GetService<DeserializationMiddleware>() ?? new DeserializationMiddleware(serializer));
        }

        pipelineBuilder.Use(new ConsumerInvocationMiddleware(mediator));
        return pipelineBuilder.Build();
    }

    private static async Task<TestDeliveryResult> ExecuteWithRetryAsync(
        ConsumerPipeline pipeline,
        ConsumerContext context,
        Type consumerType,
        int maxRetryAttempts,
        bool requeueOnFailure,
        ILogger? logger)
    {
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

    /// <summary>
    /// Gets all captured published messages of the specified type <typeparamref name="TMessage"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <returns>A read-only list of published messages.</returns>
    public IReadOnlyList<TMessage> GetPublishedMessages<TMessage>() =>
        serviceProvider.GetRequiredService<MessageTestStore>().GetPublishedMessages<TMessage>();

    /// <summary>
    /// Clears all captured messages from the in-memory test store.
    /// </summary>
    public void Clear() =>
        serviceProvider.GetRequiredService<MessageTestStore>().Clear();

    /// <summary>
    /// Asserts that at least one message of type <typeparamref name="TMessage"/> (optionally matching a condition) was published.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type to verify.</typeparam>
    /// <param name="predicate">An optional filter predicate to evaluate on each published message.</param>
    /// <returns>The first matching message instance found.</returns>
    /// <exception cref="CarotteTestAssertionException">Thrown if no published message matches the criteria.</exception>
    public TMessage ShouldHavePublished<TMessage>(Func<TMessage, bool>? predicate = null)
    {
        var messages = GetPublishedMessages<TMessage>();

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

    /// <summary>
    /// Asserts that no message of type <typeparamref name="TMessage"/> (or none matching the specified condition) was published.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type to verify.</typeparam>
    /// <param name="predicate">An optional filter predicate to evaluate on published messages.</param>
    /// <exception cref="CarotteTestAssertionException">Thrown if one or more messages matching the criteria were published.</exception>
    public void ShouldNotHavePublished<TMessage>(Func<TMessage, bool>? predicate = null)
    {
        var messages = GetPublishedMessages<TMessage>();

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

    /// <summary>
    /// Asynchronously waits until a message of type <typeparamref name="TMessage"/> matching the optional predicate is published.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type to wait for.</typeparam>
    /// <param name="predicate">An optional predicate to filter the published message.</param>
    /// <param name="timeout">The maximum time to wait (defaults to 5 seconds if not specified).</param>
    /// <param name="cancellationToken">A cancellation token to cancel waiting.</param>
    /// <returns>The matching published message.</returns>
    /// <exception cref="TimeoutException">Thrown if no matching message is published before the timeout expires.</exception>
    public async Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        Func<TMessage, bool>? predicate = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var store = serviceProvider.GetRequiredService<MessageTestStore>();
        predicate ??= _ => true;

        var existing = store.GetPublishedMessages<TMessage>().FirstOrDefault(predicate);
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
            var doubleCheck = store.GetPublishedMessages<TMessage>().FirstOrDefault(predicate);
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

    /// <summary>
    /// Asynchronously waits until any message of type <typeparamref name="TMessage"/> is published within the specified timeout.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type to wait for.</typeparam>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">A cancellation token to cancel waiting.</param>
    /// <returns>The published message.</returns>
    /// <exception cref="TimeoutException">Thrown if no message is published before the timeout expires.</exception>
    public Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForPublishedMessageAsync<TMessage>(predicate: null, timeout: (TimeSpan?)timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Asynchronously waits until a message of type <typeparamref name="TMessage"/> matching the predicate is published within the specified timeout.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type to wait for.</typeparam>
    /// <param name="predicate">A predicate to filter the published message.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">A cancellation token to cancel waiting.</param>
    /// <returns>The matching published message.</returns>
    /// <exception cref="TimeoutException">Thrown if no matching message is published before the timeout expires.</exception>
    public Task<TMessage> WaitForPublishedMessageAsync<TMessage>(
        Func<TMessage, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        WaitForPublishedMessageAsync(predicate, (TimeSpan?)timeout, cancellationToken);
}
