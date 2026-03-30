using RabbitMQ.Client;

namespace Carotte;

public interface IConnectionManager : IDisposable
{
    ValueTask<IConnection> GetConnectionAsync(string brokerName);
}

public class ConnectionManager(IDictionary<string, RabbitMQOptions> options) : IConnectionManager
{
    private readonly IDictionary<string, IConnection> _connections = new Dictionary<string, IConnection>();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async ValueTask<IConnection> GetConnectionAsync(string brokerName)
    {
        if (_connections.TryGetValue(brokerName, out var connection))
        {
            return connection;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_connections.TryGetValue(brokerName, out connection))
            {
                return connection;
            }

            if (!options.TryGetValue(brokerName, out var opt))
            {
                throw new ArgumentException($"Broker configuration not found for: {brokerName}");
            }

            var factory = new ConnectionFactory
            {
                HostName = opt.Host,
                Port = opt.Port,
                UserName = opt.UserName,
                Password = opt.Password
            };

            connection = await factory.CreateConnectionAsync();
            _connections[brokerName] = connection;
            return connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
        _semaphore.Dispose();
    }
}
