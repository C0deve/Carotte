using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;

namespace Carotte;

/// <summary>
/// Defines a non-generic contract to invoke a message handler dynamically.
/// This acts as a type-erased bridge allowing heterogeneous generic invokers
/// to be stored in a non-generic collection without reflection on the hot path.
/// </summary>
internal interface IMessageInvoker
{
    /// <summary>
    /// Invokes the strongly-typed <see cref="IConsumer{TMessage}.HandleAsync"/> method on the target consumer.
    /// </summary>
    /// <param name="consumer">The consumer instance resolved from the DI service provider.</param>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous message processing operation.</returns>
    Task InvokeAsync(object consumer, object message, CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed generic invoker implementing <see cref="IMessageInvoker"/>.
/// By closing over <typeparamref name="TMessage"/> at startup, it performs direct casting
/// and interface dispatch at runtime, achieving zero heap allocations and near-native execution speed.
/// </summary>
/// <typeparam name="TMessage">The concrete message type handled by the consumer.</typeparam>
internal sealed class MessageInvoker<TMessage> : IMessageInvoker
{
    /// <inheritdoc/>
    public Task InvokeAsync(object consumer, object message, CancellationToken cancellationToken) =>
        ((IConsumer<TMessage>)consumer).HandleAsync((TMessage)message, cancellationToken);
}

/// <summary>
/// Mediates incoming RabbitMQ messages to their corresponding <see cref="IConsumer{TMessage}"/> implementations.
/// Pre-computes typed invokers at startup and optimizes dispatch with a zero-lookup fast-path for single-message consumers.
/// </summary>
internal sealed class ConsumerMediator(
    IServiceProvider serviceProvider,
    IMessageTypeResolver? messageTypeResolver = null)
{
    private readonly IMessageTypeResolver _messageTypeResolver = messageTypeResolver ?? MessageTypeResolver.Default;
    private Type? _consumerType;
    private readonly Dictionary<Type, IMessageInvoker> _invokers = [];

    // Fast-path cache: bypasses dictionary lookups for consumers handling exactly one message type (the most common case).
    private IMessageInvoker? _singleInvoker;
    private Type? _singleMessageType;

    /// <summary>
    /// Initializes the mediator for a given consumer type.
    /// Discovers all implemented <see cref="IConsumer{TMessage}"/> interfaces and instantiates closed <see cref="MessageInvoker{TMessage}"/> adapters.
    /// </summary>
    /// <param name="consumerType">The concrete consumer class type.</param>
    public void Initialize(Type consumerType)
    {
        _consumerType = consumerType;
        _invokers.Clear();
        _singleInvoker = null;
        _singleMessageType = null;

        // Scan consumer interfaces for all implemented IConsumer<TMessage> variants
        var consumerInterfaces = _consumerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>));

        foreach (var i in consumerInterfaces)
        {
            var messageType = i.GetGenericArguments()[0];
            // Instantiate the closed generic MessageInvoker<TMessage> once at startup
            var invokerType = typeof(MessageInvoker<>).MakeGenericType(messageType);
            var invoker = (IMessageInvoker)Activator.CreateInstance(invokerType)!;
            _invokers[messageType] = invoker;
        }

        // Set up the single-message fast-path if applicable
        if (_invokers.Count != 1)
            return;

        var singlePair = _invokers.First();
        _singleMessageType = singlePair.Key;
        _singleInvoker = singlePair.Value;
    }

    /// <summary>
    /// Generic convenience overload to initialize the mediator for <typeparamref name="TConsumer"/>.
    /// </summary>
    /// <typeparam name="TConsumer">The concrete consumer class type.</typeparam>
    public void Initialize<TConsumer>() where TConsumer : class => Initialize(typeof(TConsumer));

    /// <summary>
    /// Returns all message types handled by the registered consumer.
    /// </summary>
    public IEnumerable<Type> GetHandledMessageTypes() => _invokers.Keys;

    /// <summary>
    /// Resolves the concrete message <see cref="Type"/> from delivery headers/metadata.
    /// </summary>
    /// <param name="ea">The RabbitMQ basic delivery event args.</param>
    /// <returns>The resolved message type, or <c>null</c> if unrecognized.</returns>
    public Type? ResolveMessageType(BasicDeliverEventArgs ea) =>
        _messageTypeResolver.ResolveType(ea.BasicProperties.Type, _invokers.Keys);

    /// <summary>
    /// Creates an isolated async service scope for resolving scoped dependencies per message.
    /// </summary>
    internal AsyncServiceScope CreateMessageScope() => serviceProvider.CreateAsyncScope();

    /// <summary>
    /// Dispatches a deserialized message to the resolved consumer instance.
    /// Uses the single-message fast-path when available or falls back to dictionary lookup.
    /// </summary>
    /// <param name="messageServiceProvider">The scoped service provider for the current message.</param>
    /// <param name="messageType">The runtime message type.</param>
    /// <param name="message">The deserialized message payload object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the handling operation.</returns>
    internal Task InvokeAsync(
        IServiceProvider messageServiceProvider,
        Type messageType,
        object message,
        CancellationToken cancellationToken)
    {
        if (_consumerType is null)
            return Task.CompletedTask;

        // 1. Resolve the typed invoker (fast-path for single-message, dictionary lookup otherwise)
        var invoker = _singleInvoker is not null && _singleMessageType == messageType
            ? _singleInvoker
            : _invokers.GetValueOrDefault(messageType);

        if (invoker is null)
            return Task.CompletedTask;

        // 2. Resolve the consumer instance from the per-message scope and dispatch directly
        var handler = messageServiceProvider.GetRequiredService(_consumerType);
        return invoker.InvokeAsync(handler, message, cancellationToken);
    }
}
