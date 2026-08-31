namespace Carotte;

internal static class CarotteBuilderValidator
{
    public static ValidationResult Validate(MessageBrokerSettings settings)
    {
        if (settings.Brokers.Count == 0) return ValidationResult.Failure(new NoBrokerRegistered());

        var consumerValidation = ValidateConsumers(settings);
        if (!consumerValidation.IsSuccess) return consumerValidation;

        var producerValidation = ValidatePublishers(settings);
        if (!producerValidation.IsSuccess) return producerValidation;

        var topologyValidation = ValidateExchangeDeclarations(settings);
        if (!topologyValidation.IsSuccess) return topologyValidation;

        return ValidationResult.Success();
    }

    private static ValidationResult ValidatePublishers(MessageBrokerSettings settings)
    {
        var errors = (
                from publisher in settings.Publishers
                where !settings.Brokers.ContainsKey(publisher.Broker)
                select new BrokerNotFoundForPublisher(publisher.Broker, publisher.MessageType.Name))
            .Cast<ConfigurationError>()
            .ToList();

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static ValidationResult ValidateConsumers(MessageBrokerSettings settings)
    {
        var errors =
            (from consumer in settings.Consumers
             where !settings.Brokers.ContainsKey(consumer.Broker)
             select new BrokerNotFoundForConsumer(consumer.Broker, consumer.ConsumerType.Name))
            .Cast<ConfigurationError>()
            .ToList();

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static ValidationResult ValidateExchangeDeclarations(MessageBrokerSettings settings)
    {
        var (_, consumers, publishers) = settings;
        var declarations = publishers
            .Where(publisher => publisher.DeclareExchange)
            .Select(publisher => new ExchangeDeclaration(
                publisher.Broker,
                publisher.ExchangePublication,
                publisher.ExchangeType,
                publisher.Durable,
                publisher.AutoDelete))
            .ToList();

        foreach (var consumer in consumers)
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