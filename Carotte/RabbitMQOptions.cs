using System.Security.Authentication;

namespace Carotte;

public record RabbitMqSslOptions
{
    public bool Enabled { get; set; }
    public string? ServerName { get; set; }
    public bool AcceptUntrustedCertificates { get; set; }
    public SslProtocols? Version { get; set; }
}

public record RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string? ConnectionString { get; set; }
    public List<string> Hosts { get; set; } = [];
    public ushort DefaultPrefetchCount { get; set; } = 1;
    public TimeSpan? RequestedHeartbeat { get; set; }
    public TimeSpan? RequestedConnectionTimeout { get; set; }
    public TimeSpan? ContinuationTimeout { get; set; }
    public TimeSpan? NetworkRecoveryInterval { get; set; }
    public string? ClientProvidedName { get; set; }
    public RabbitMqSslOptions? Ssl { get; set; }
}
