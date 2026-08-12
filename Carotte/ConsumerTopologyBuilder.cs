namespace Carotte;

internal static class ConsumerTopologyBuilder
{
    public static async Task BuildAsync(
        IRabbitMqClient rabbitMqClient,
        IConsumerTopology topology,
        CancellationToken cancellationToken)
    {
        switch (topology)
        {
            case ConsumerConventionTopology conventionTopology:
                await SetupConventionTopologyAsync(rabbitMqClient, conventionTopology, cancellationToken);
                break;
            case ConsumerAttributeTopology attributeTopology:
                await SetupAttributeTopologyAsync(rabbitMqClient, attributeTopology, cancellationToken);
                break;
        }
    }

    private static async Task SetupConventionTopologyAsync(IRabbitMqClient rabbitMqClient,
        ConsumerConventionTopology topology,
        CancellationToken cancellationToken)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);

        await rabbitMqClient.ExchangeDeclareAsync(
            exchange: topology.ConsumerExchangeName,
            type: "fanout",
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await SetupDeadLetterExchangeAsync(rabbitMqClient, errorStrategy, cancellationToken);

        await rabbitMqClient.QueueDeclareAsync(
            queue: topology.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: CreateQueueArguments(errorStrategy),
            cancellationToken: cancellationToken);

        await rabbitMqClient.QueueBindAsync(
            queue: topology.Queue,
            exchange: topology.ConsumerExchangeName,
            routingKey: "",
            cancellationToken: cancellationToken);

        foreach (var messageExchange in topology.MessageExchangeNames)
        {
            // We declare the message exchange here to ensure it exists before binding.
            // This prevents errors if the consumer starts before any producer has published a message.
            // Note: This assumes the convention that all message exchanges are of type 'fanout'.
            await rabbitMqClient.ExchangeDeclareAsync(
                exchange: messageExchange,
                type: "fanout",
                durable: true,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);

            await rabbitMqClient.ExchangeBindAsync(
                destination: topology.ConsumerExchangeName,
                source: messageExchange,
                routingKey: "",
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SetupAttributeTopologyAsync(
        IRabbitMqClient rabbitMqClient,
        ConsumerAttributeTopology topology,
        CancellationToken cancellationToken)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);

        await SetupDeadLetterExchangeAsync(rabbitMqClient, errorStrategy, cancellationToken);

        foreach (var exchange in topology.Bindings
                     .Where(binding => binding.DeclareExchange && !string.IsNullOrWhiteSpace(binding.ExchangeSource))
                     .DistinctBy(binding => binding.ExchangeSource))
        {
            await rabbitMqClient.ExchangeDeclareAsync(
                exchange: exchange.ExchangeSource,
                type: exchange.ExchangeType.ToString().ToLowerInvariant(),
                durable: exchange.Durable,
                autoDelete: exchange.AutoDelete,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);
        }

        await rabbitMqClient.QueueDeclareAsync(
            queue: topology.Queue,
            durable: topology.QueueDurable,
            exclusive: topology.QueueExclusive,
            autoDelete: topology.QueueAutoDelete,
            arguments: CreateQueueArguments(errorStrategy),
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        foreach (var binding in topology.Bindings)
        {
            await rabbitMqClient.QueueBindAsync(
                queue: topology.Queue,
                exchange: binding.ExchangeSource,
                routingKey: binding.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SetupDeadLetterExchangeAsync(
        IRabbitMqClient rabbitMqClient,
        ConsumerErrorStrategy errorStrategy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(errorStrategy.DeadLetterExchange) ||
            string.IsNullOrWhiteSpace(errorStrategy.DeadLetterQueue))
        {
            return;
        }

        await rabbitMqClient.ExchangeDeclareAsync(
            exchange: errorStrategy.DeadLetterExchange,
            type: "fanout",
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await rabbitMqClient.QueueDeclareAsync(
            queue: errorStrategy.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await rabbitMqClient.QueueBindAsync(
            queue: errorStrategy.DeadLetterQueue,
            exchange: errorStrategy.DeadLetterExchange,
            routingKey: errorStrategy.DeadLetterRoutingKey ?? string.Empty,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);
    }

    private static IDictionary<string, object?>? CreateQueueArguments(ConsumerErrorStrategy errorStrategy)
    {
        if (string.IsNullOrWhiteSpace(errorStrategy.DeadLetterExchange))
        {
            return null;
        }

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = errorStrategy.DeadLetterExchange
        };

        if (!string.IsNullOrWhiteSpace(errorStrategy.DeadLetterRoutingKey))
        {
            arguments["x-dead-letter-routing-key"] = errorStrategy.DeadLetterRoutingKey;
        }

        return arguments;
    }
}
