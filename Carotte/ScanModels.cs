using System.Collections.ObjectModel;

namespace Carotte;

internal readonly record struct MessageBrokerSettings(
    ReadOnlyDictionary<string, BrokerInfos> Brokers,
    ReadOnlyCollection<ConsumerInfo> Consumers,
    ReadOnlyCollection<ProducerInfo> Producers);



public record ValidationResult
{
    public bool IsSuccess => Errors.Count == 0;
    public IReadOnlyCollection<ConfigurationError> Errors { get; private init; } = [];

    public static ValidationResult Success() => new();
    public static ValidationResult Failure(ConfigurationError error) => new() { Errors = [error] };
    public static ValidationResult Failure(IEnumerable<ConfigurationError> errors) => new() { Errors = errors.ToList().AsReadOnly() };
}

public abstract record ConfigurationError(string Message);

public record NoBrokerRegistered() : ConfigurationError("No broker registered. At least one broker must be registered.");

public record BrokerNotFoundForConsumer(string BrokerName, string ConsumerName) 
    : ConfigurationError($"No broker registered with name '{BrokerName}' for consumer '{ConsumerName}'");

public record BrokerNotFoundForPublisher(string BrokerName, string MessageName) 
    : ConfigurationError($"No broker registered with name '{BrokerName}' for publisher of message '{MessageName}'");

public record ConflictingExchangeDeclaration(string BrokerName, string ExchangeName)
    : ConfigurationError($"Conflicting declarations for exchange '{ExchangeName}' on broker '{BrokerName}'");
