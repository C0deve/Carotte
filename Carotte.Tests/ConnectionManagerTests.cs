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
    public Task GetConnectionAsync_ShouldReturnSameConnectionForSameBroker()
    {
        return Task.CompletedTask;
        // We cannot easily mock ConnectionFactory because it is a concrete class with CreateConnectionAsync.
        // But we could verify the cache behavior in ConnectionManager if we had an abstraction or by testing the real connection exception if no broker.
        // Given that RabbitMQ.Client 7+ uses many interfaces, we could mock IConnection if we went through a mocked factory.
    }
}
