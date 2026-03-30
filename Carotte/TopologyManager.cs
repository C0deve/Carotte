namespace Carotte;

public interface ITopologyManager
{
    Task DeclareTopologyAsync(string brokerName, IEnumerable<ExchangeOptions> exchanges, IEnumerable<QueueOptions> queues, IEnumerable<BindingOptions> bindings);
}

public class TopologyManager(IConnectionManager connectionManager) : ITopologyManager
{
    public async Task DeclareTopologyAsync(string brokerName, IEnumerable<ExchangeOptions> exchanges, IEnumerable<QueueOptions> queues, IEnumerable<BindingOptions> bindings)
    {
        var connection = await connectionManager.GetConnectionAsync(brokerName);
        using var channel = await connection.CreateChannelAsync();

        foreach (var exchange in exchanges)
        {
            await channel.ExchangeDeclareAsync(
                exchange: exchange.Name,
                type: exchange.Type.ToString().ToLowerInvariant(),
                durable: exchange.Durable,
                autoDelete: exchange.AutoDelete);
        }

        foreach (var queue in queues)
        {
            await channel.QueueDeclareAsync(
                queue: queue.Name,
                durable: queue.Durable,
                exclusive: queue.Exclusive,
                autoDelete: queue.AutoDelete);
        }

        foreach (var binding in bindings)
        {
            await channel.QueueBindAsync(
                queue: binding.QueueName,
                exchange: binding.ExchangeName,
                routingKey: binding.RoutingKey);
        }
    }
}
