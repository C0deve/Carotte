using RabbitMQ.Client;

namespace Carotte;

internal sealed class ConnectionManager(IDictionary<string, RabbitMqOptions> options) : IConnectionManager
{
    private readonly IDictionary<string, IConnection> _connections = new Dictionary<string, IConnection>();
    private readonly Dictionary<string, int> _activeHosts = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async ValueTask<IConnection> GetConnectionAsync(string brokerName)
    {
        if (_connections.TryGetValue(brokerName, out var connection) && connection.IsOpen)
        {
            return connection;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_connections.TryGetValue(brokerName, out connection))
            {
                if (connection.IsOpen)
                {
                    return connection;
                }

                connection.Dispose();
                _connections.Remove(brokerName);
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

    public async Task RegisterHostAsync(string brokerName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var count = _activeHosts.GetValueOrDefault(brokerName, 0);
            _activeHosts[brokerName] = count + 1;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UnregisterHostAsync(string brokerName)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_activeHosts.TryGetValue(brokerName, out var count))
            {
                count--;
                if (count <= 0)
                {
                    _activeHosts.Remove(brokerName);
                    if (_connections.TryGetValue(brokerName, out var connection))
                    {
                        await connection.CloseAsync();
                        _connections.Remove(brokerName);
                    }
                }
                else
                {
                    _activeHosts[brokerName] = count;
                }
            }
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
        _activeHosts.Clear();
        _semaphore.Dispose();
    }
}
