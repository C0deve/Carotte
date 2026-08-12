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

        var topologyValidation = ValidateExchangeDeclarations(settings);
        if (!topologyValidation.IsSuccess) return topologyValidation;
        
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

    private static ValidationResult ValidateExchangeDeclarations(MessageBrokerSettings settings)
    {
        var declarations = new List<ExchangeDeclaration>();

        foreach (var producer in settings.Producers.Where(producer => producer.DeclareExchange))
        {
            declarations.Add(new ExchangeDeclaration(
                producer.Broker,
                producer.ExchangePublication,
                producer.ExchangeType,
                producer.Durable,
                producer.AutoDelete));
        }

        foreach (var consumer in settings.Consumers)
        {
            switch (consumer.Topology)
            {
                case ConsumerConventionTopology convention:
                    declarations.Add(new ExchangeDeclaration(
                        consumer.Broker,
                        convention.ConsumerExchangeName,
                        ExchangeType.Fanout,
                        Durable: true,
                        AutoDelete: false));
                    declarations.AddRange(convention.MessageExchangeNames.Select(name => new ExchangeDeclaration(
                        consumer.Broker,
                        name,
                        ExchangeType.Fanout,
                        Durable: true,
                        AutoDelete: false)));
                    break;

                case ConsumerAttributeTopology attribute:
                    declarations.AddRange(attribute.Bindings
                        .Where(binding => binding.DeclareExchange && !string.IsNullOrWhiteSpace(binding.ExchangeSource))
                        .Select(binding => new ExchangeDeclaration(
                            consumer.Broker,
                            binding.ExchangeSource,
                            binding.ExchangeType,
                            binding.Durable,
                            binding.AutoDelete)));
                    break;
            }
        }

        var errors = declarations
            .GroupBy(declaration => (declaration.Broker, declaration.Name))
            .Where(group => group
                .Select(declaration => (declaration.Type, declaration.Durable, declaration.AutoDelete))
                .Distinct()
                .Skip(1)
                .Any())
            .Select(group => new ConflictingExchangeDeclaration(group.Key.Broker, group.Key.Name))
            .ToList();

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    private readonly record struct ExchangeDeclaration(
        string Broker,
        string Name,
        ExchangeType Type,
        bool Durable,
        bool AutoDelete);
}
