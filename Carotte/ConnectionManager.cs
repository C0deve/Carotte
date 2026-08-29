using System.Net.Security;
using RabbitMQ.Client;

namespace Carotte;

/// <summary>
/// Manages RabbitMQ broker connections with thread-safe pooling, host reference counting,
/// and automated connection lifecycle management.
/// </summary>
internal sealed class ConnectionManager(IDictionary<string, RabbitMqOptions> options) : IConnectionManager
{
    private readonly IDictionary<string, IConnection> _connections = new Dictionary<string, IConnection>();
    private readonly Dictionary<string, int> _activeHosts = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Gets an open <see cref="IConnection"/> for the specified broker name.
    /// Reuses existing open connections or creates a new connection asynchronously.
    /// </summary>
    /// <param name="brokerName">The configured broker name.</param>
    /// <returns>An active <see cref="IConnection"/>.</returns>
    public async ValueTask<IConnection> GetConnectionAsync(string brokerName)
    {
        // Fast-path: return existing open connection
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

                try
                {
                    connection.Dispose();
                }
                catch (Exception)
                {
                }

                _connections.Remove(brokerName);
            }

            if (!options.TryGetValue(brokerName, out var opt))
            {
                throw new ArgumentException($"Broker configuration not found for: {brokerName}");
            }

            var factory = CreateConnectionFactory(opt);

            if (opt.Hosts.Count > 0)
            {
                connection = await factory.CreateConnectionAsync(opt.Hosts, opt.ClientProvidedName);
            }
            else
            {
                connection = await factory.CreateConnectionAsync(opt.ClientProvidedName);
            }

            _connections[brokerName] = connection;
            return connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Builds a configured <see cref="ConnectionFactory"/> from the provided <see cref="RabbitMqOptions"/>.
    /// Supports URI connection strings, individual host/port credentials, SSL/TLS, and timeouts.
    /// </summary>
    internal static ConnectionFactory CreateConnectionFactory(RabbitMqOptions opt)
    {
        var factory = new ConnectionFactory();

        if (!string.IsNullOrWhiteSpace(opt.ConnectionString))
        {
            factory.Uri = new Uri(opt.ConnectionString);
        }
        else
        {
            factory.HostName = opt.Host;
            factory.Port = opt.Port;
            factory.VirtualHost = opt.VirtualHost;
            factory.UserName = opt.UserName;
            factory.Password = opt.Password;
        }

        if (opt.ClientProvidedName != null)
        {
            factory.ClientProvidedName = opt.ClientProvidedName;
        }

        if (opt.RequestedHeartbeat.HasValue)
        {
            factory.RequestedHeartbeat = opt.RequestedHeartbeat.Value;
        }

        if (opt.RequestedConnectionTimeout.HasValue)
        {
            factory.RequestedConnectionTimeout = opt.RequestedConnectionTimeout.Value;
        }

        if (opt.ContinuationTimeout.HasValue)
        {
            factory.ContinuationTimeout = opt.ContinuationTimeout.Value;
        }

        if (opt.NetworkRecoveryInterval.HasValue)
        {
            factory.NetworkRecoveryInterval = opt.NetworkRecoveryInterval.Value;
        }

        if (opt.Ssl != null)
        {
            factory.Ssl.Enabled = opt.Ssl.Enabled;
            if (opt.Ssl.ServerName != null)
            {
                factory.Ssl.ServerName = opt.Ssl.ServerName;
            }
            if (opt.Ssl.AcceptUntrustedCertificates)
            {
                factory.Ssl.AcceptablePolicyErrors |= SslPolicyErrors.RemoteCertificateNameMismatch
                                                   | SslPolicyErrors.RemoteCertificateChainErrors
                                                   | SslPolicyErrors.RemoteCertificateNotAvailable;
            }
            if (opt.Ssl.Version.HasValue)
            {
                factory.Ssl.Version = opt.Ssl.Version.Value;
            }
        }

        return factory;
    }

    /// <summary>
    /// Increments the active consumer/publisher host reference count for a broker.
    /// </summary>
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

    /// <summary>
    /// Decrements the active consumer/publisher host reference count for a broker.
    /// Automatically closes and removes the underlying connection when the count drops to zero.
    /// </summary>
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
                        try
                        {
                            if (connection.IsOpen)
                            {
                                await connection.CloseAsync();
                            }
                            connection.Dispose();
                        }
                        catch (Exception)
                        {
                        }

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

    /// <summary>
    /// Closes and disposes all managed connections.
    /// </summary>
    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception)
            {
            }
        }
        _connections.Clear();
        _activeHosts.Clear();
        _semaphore.Dispose();
    }
}
