using System.Collections.ObjectModel;

namespace Carotte;

internal static class CarotteBuilderValidator
{
    public static ValidationResult Validate(MessageBrokerSettings settings)
    {
        if (settings.Brokers.Count == 0) return ValidationResult.Failure(new NoBrokerRegistered());
        
        var consumerValidation = ValidateConsumers(settings);
        if (!consumerValidation.IsSuccess) return consumerValidation;

        var producerValidation = ValidateProducers(settings);
        if (!producerValidation.IsSuccess) return producerValidation;
        
        return ValidationResult.Success();
    }

    private static ValidationResult ValidateProducers(MessageBrokerSettings settings)
    {
        var errors = new List<ConfigurationError>();
        foreach (var producer in settings.Producers)
        {
            if (!settings.Brokers.ContainsKey(producer.Broker))
            {
                errors.Add(new BrokerNotFoundForPublisher(producer.Broker, producer.MessageType.Name));
            }
        }
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    private static ValidationResult ValidateConsumers(MessageBrokerSettings settings)
    {   
        var errors = new List<ConfigurationError>();
        foreach (var consumer in settings.Consumers)
        {
            if (!settings.Brokers.ContainsKey(consumer.Broker))
            {
                errors.Add(new BrokerNotFoundForConsumer(consumer.Broker, consumer.ConsumerType.Name));
            }
        }
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}