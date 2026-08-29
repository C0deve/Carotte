namespace Carotte;

/// <summary>
/// Responsible for declaring RabbitMQ topology (exchanges, queues, bindings, and dead-letter entities)
/// based on convention-based or attribute-based configurations.
/// </summary>
internal static class ConsumerTopologyBuilder
{
    /// <summary>
    /// Builds and applies the RabbitMQ topology for the specified consumer topology definition.
    /// </summary>
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

    /// <summary>
    /// Configures convention-based topology:
    /// - Creates consumer exchange (fanout) and message exchanges (fanout)
    /// - Binds message exchanges to consumer exchange (exchange-to-exchange binding)
    /// - Declares consumer queue with DLX arguments and binds it to consumer exchange
    /// </summary>
    private static async Task SetupConventionTopologyAsync(IRabbitMqClient rabbitMqClient,
        ConsumerConventionTopology topology,
        CancellationToken cancellationToken)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);

        // 1. Declare consumer-level exchange
        await rabbitMqClient.ExchangeDeclareAsync(
            exchange: topology.ConsumerExchangeName,
            type: "fanout",
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // 2. Declare Dead Letter Exchange and Queue if required
        await SetupDeadLetterExchangeAsync(rabbitMqClient, errorStrategy, cancellationToken);

        // 3. Declare consumer queue with dead letter arguments
        await rabbitMqClient.QueueDeclareAsync(
            queue: topology.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: CreateQueueArguments(errorStrategy, topology.Arguments),
            cancellationToken: cancellationToken);

        // 4. Bind consumer queue to consumer exchange
        await rabbitMqClient.QueueBindAsync(
            queue: topology.Queue,
            exchange: topology.ConsumerExchangeName,
            routingKey: "",
            cancellationToken: cancellationToken);

        // 5. Declare message-type exchanges and bind each to the consumer exchange (E2E bindings)
        foreach (var messageExchange in topology.MessageExchangeNames)
        {
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

    /// <summary>
    /// Configures attribute-based topology:
    /// - Sets up dead-letter exchange/queue if applicable
    /// - Declares all explicitly bound exchanges
    /// - Declares consumer queue with custom/dead-letter arguments
    /// - Sets up queue-to-exchange bindings
    /// </summary>
    private static async Task SetupAttributeTopologyAsync(
        IRabbitMqClient rabbitMqClient,
        ConsumerAttributeTopology topology,
        CancellationToken cancellationToken)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);

        // 1. Declare Dead Letter Exchange and Queue if applicable
        await SetupDeadLetterExchangeAsync(rabbitMqClient, errorStrategy, cancellationToken);

        // 2. Declare exchanges configured in bindings
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

        // 3. Declare consumer queue
        await rabbitMqClient.QueueDeclareAsync(
            queue: topology.Queue,
            durable: topology.QueueDurable,
            exclusive: topology.QueueExclusive,
            autoDelete: topology.QueueAutoDelete,
            arguments: CreateQueueArguments(errorStrategy, topology.Arguments),
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        // 4. Bind queue to declared exchanges
        foreach (var binding in topology.Bindings.Where(binding => !string.IsNullOrWhiteSpace(binding.ExchangeSource)))
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

    /// <summary>
    /// Declares dead-letter exchange, dead-letter queue and binds them together.
    /// Skipped if failure action is set to Requeue or if dead-letter names are empty.
    /// </summary>
    private static async Task SetupDeadLetterExchangeAsync(
        IRabbitMqClient rabbitMqClient,
        ConsumerErrorStrategy errorStrategy,
        CancellationToken cancellationToken)
    {
        if (errorStrategy.FailureAction == ConsumerFailureAction.Requeue ||
            string.IsNullOrWhiteSpace(errorStrategy.DeadLetterExchange) ||
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

    /// <summary>
    /// Merges user-defined queue arguments with RabbitMQ dead-lettering headers (x-dead-letter-exchange, x-dead-letter-routing-key).
    /// </summary>
    internal static IDictionary<string, object?>? CreateQueueArguments(
        ConsumerErrorStrategy errorStrategy,
        IReadOnlyDictionary<string, object> customArguments)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (k, v) in customArguments)
        {
            arguments[k] = v;
        }

        if (errorStrategy.FailureAction != ConsumerFailureAction.Requeue)
        {
            if (!string.IsNullOrWhiteSpace(errorStrategy.DeadLetterExchange) && !arguments.ContainsKey("x-dead-letter-exchange"))
            {
                arguments["x-dead-letter-exchange"] = errorStrategy.DeadLetterExchange;
            }

            if (!string.IsNullOrWhiteSpace(errorStrategy.DeadLetterRoutingKey) && !arguments.ContainsKey("x-dead-letter-routing-key"))
            {
                arguments["x-dead-letter-routing-key"] = errorStrategy.DeadLetterRoutingKey;
            }
        }

        return arguments.Count > 0 ? arguments : null;
    }
}
