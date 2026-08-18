using System.Net.Security;
using System.Security.Authentication;
using Shouldly;

namespace Carotte.Tests;

public class ConnectionManagerTests
{
    [Fact]
    public async Task GetConnectionAsync_ShouldThrowIfBrokerNotFound()
    {
        var options = new Dictionary<string, RabbitMqOptions>();
        var manager = new ConnectionManager(options);

        await Should.ThrowAsync<ArgumentException>(() => manager.GetConnectionAsync("Unknown").AsTask());
    }

    [Fact]
    public void CreateConnectionFactory_ShouldMapStandardProperties()
    {
        var options = new RabbitMqOptions
        {
            Host = "rabbitmq.local",
            Port = 5673,
            VirtualHost = "/custom-vhost",
            UserName = "admin",
            Password = "secret",
            ClientProvidedName = "TestApp",
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(15),
            ContinuationTimeout = TimeSpan.FromSeconds(45),
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        var factory = ConnectionManager.CreateConnectionFactory(options);

        factory.HostName.ShouldBe("rabbitmq.local");
        factory.Port.ShouldBe(5673);
        factory.VirtualHost.ShouldBe("/custom-vhost");
        factory.UserName.ShouldBe("admin");
        factory.Password.ShouldBe("secret");
        factory.ClientProvidedName.ShouldBe("TestApp");
        factory.RequestedHeartbeat.ShouldBe(TimeSpan.FromSeconds(30));
        factory.RequestedConnectionTimeout.ShouldBe(TimeSpan.FromSeconds(15));
        factory.ContinuationTimeout.ShouldBe(TimeSpan.FromSeconds(45));
        factory.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void CreateConnectionFactory_ShouldMapConnectionString()
    {
        var options = new RabbitMqOptions
        {
            ConnectionString = "amqp://user:pass@remotehost:5679/sales"
        };

        var factory = ConnectionManager.CreateConnectionFactory(options);

        factory.HostName.ShouldBe("remotehost");
        factory.Port.ShouldBe(5679);
        factory.VirtualHost.ShouldBe("sales");
        factory.UserName.ShouldBe("user");
        factory.Password.ShouldBe("pass");
    }

    [Fact]
    public void CreateConnectionFactory_ShouldMapSslOptions()
    {
        var options = new RabbitMqOptions
        {
            Host = "secure.rabbit",
            Ssl = new RabbitMqSslOptions
            {
                Enabled = true,
                ServerName = "secure.rabbit",
                AcceptUntrustedCertificates = true,
                Version = SslProtocols.Tls13
            }
        };

        var factory = ConnectionManager.CreateConnectionFactory(options);

        factory.Ssl.Enabled.ShouldBeTrue();
        factory.Ssl.ServerName.ShouldBe("secure.rabbit");
        factory.Ssl.Version.ShouldBe(SslProtocols.Tls13);
        factory.Ssl.AcceptablePolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch).ShouldBeTrue();
    }
}
