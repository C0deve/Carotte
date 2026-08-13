using RabbitMQ.Client;

namespace Carotte;

public interface IConnectionManager : IDisposable
{
    ValueTask<IConnection> GetConnectionAsync(string brokerName);
    Task RegisterHostAsync(string brokerName);
    Task UnregisterHostAsync(string brokerName);
}