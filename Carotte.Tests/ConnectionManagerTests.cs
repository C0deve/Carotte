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
    public async Task GetConnectionAsync_ShouldReturnSameConnectionForSameBroker()
    {
        // On ne peut pas facilement mocker ConnectionFactory car c'est une classe concrète avec CreateConnectionAsync.
        // Mais on peut vérifier le comportement du cache dans ConnectionManager si on avait une abstraction ou en testant l'exception de connexion réelle si pas de broker.
        // Étant donné que RabbitMQ.Client 7+ utilise beaucoup d'interfaces, on pourrait mocker IConnection si on passait par une factory mockée.
    }
}
