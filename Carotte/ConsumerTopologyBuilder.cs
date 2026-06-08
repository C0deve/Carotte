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
        await rabbitMqClient.ExchangeDeclareAsync(
            exchange: topology.ConsumerExchangeName,
            type: "fanout",
            durable: true,
            autoDelete: false,
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
        await rabbitMqClient.QueueDeclareAsync(
            queue: topology.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
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
}