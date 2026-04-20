using Microsoft.Extensions.Logging;

namespace Carotte;

internal static partial class RabbitMqLoggingExtensions
{
    [LoggerMessage(LogLevel.Information, "Creating new channel for broker {Broker}")]
    public static partial void LogCreatingNewChannelForBroker(this ILogger logger, string broker);

    [LoggerMessage(LogLevel.Information, "Disposing channel for broker {Broker}")]
    public static partial void LogDisposingChannelForBroker(this ILogger logger, string broker);

    [LoggerMessage(LogLevel.Information, "Starting consumption on queue {QueueName} for broker {Broker}")]
    public static partial void LogStartingConsumptionOnQueue(this ILogger logger, string queueName, string broker);

    [LoggerMessage(LogLevel.Information, "Declaring queue {QueueName} on broker {Broker}")]
    public static partial void LogDeclaringQueue(this ILogger logger, string queueName, string broker);

    [LoggerMessage(LogLevel.Information, "Declaring exchange {ExchangeName} on broker {Broker}")]
    public static partial void LogDeclaringExchange(this ILogger logger, string exchangeName, string broker);

    [LoggerMessage(LogLevel.Information, "Binding queue {QueueName} to exchange {ExchangeName} with routing key {RoutingKey} on broker {Broker}")]
    public static partial void LogBindingQueueToExchange(this ILogger logger, string queueName, string exchangeName, string routingKey, string broker);

    [LoggerMessage(LogLevel.Debug, "Publishing message {MessageType} to exchange {ExchangeName} with routing key {RoutingKey} on broker {Broker}")]
    public static partial void LogPublishingMessage(this ILogger logger, string messageType, string exchangeName, string routingKey, string broker);

    [LoggerMessage(LogLevel.Information, "Starting RabbitMqConsumerHost for {ConsumerType} on broker {Broker}")]
    public static partial void LogStartingRabbitmqConsumerHost(this ILogger logger, string consumerType, string broker);

    [LoggerMessage(LogLevel.Information, "Stopping RabbitMqConsumerHost for {ConsumerType}")]
    public static partial void LogStoppingRabbitmqConsumerHost(this ILogger logger, string consumerType);

    [LoggerMessage(LogLevel.Information, "Opening channel for {ConsumerType} on broker {Broker}")]
    public static partial void LogOpeningChannel(this ILogger logger, string consumerType, string broker);

    [LoggerMessage(LogLevel.Information, "Setting up topology for {ConsumerType}")]
    public static partial void LogSettingUpTopology(this ILogger logger, string consumerType);
}
