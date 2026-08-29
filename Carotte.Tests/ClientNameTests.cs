using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Moq;

namespace Carotte.Tests;

public class ClientNameTests
{
    [Fact]
    public void Extensions_WithClientName_ShouldIncludePrefix()
    {
        "MyConsumer".ToConsumerExchangeName("MyService").ShouldBe("x.sub.my-service.my-consumer");
        "MyConsumer".ToConsumerQueueName("MyService").ShouldBe("q.my-service.my-consumer");
    }

    [Fact]
    public void Extensions_WithoutClientName_ShouldNotIncludePrefix()
    {
        "MyConsumer".ToConsumerExchangeName().ShouldBe("x.sub.my-consumer");
        "MyConsumer".ToConsumerQueueName().ShouldBe("q.my-consumer");
    }

    [Fact]
    public async Task AddCarotte_WithClientName_ShouldUsePrefixedNames()
    {
        // Arrange
        var services = new ServiceCollection();
        var rabbitMqClient = new Mock<IRabbitMqClient>();

        services.AddCarotte(builder =>
        {
            builder
                .SetClientName("OrderApi")
                .AddBroker("test-broker", _ => { })
                .AddAssemblies(typeof(ClientNameTests).Assembly)
                .AddNamespaces("Carotte.Tests");
        });

        services.AddSingleton(rabbitMqClient.Object);

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();
        var host = hostedServices.FirstOrDefault(h => h.GetType().IsGenericType &&
                                            h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                            h.GetType().GetGenericArguments()[0] == typeof(ClientNameTestConsumer));

        host.ShouldNotBeNull();

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        var expectedExchange = "x.sub.order-api.client-name-test-consumer";
        var expectedQueue = "q.order-api.client-name-test-consumer";
        var expectedDeadLetterExchange = "x.dlx.order-api.client-name-test-consumer";

        // Verify exchange declaration
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Verify queue declaration
        rabbitMqClient.Verify(c => c.QueueDeclareAsync(
            expectedQueue,
            true,
            false,
            false,
            It.Is<IDictionary<string, object?>>(args =>
                (string)args["x-dead-letter-exchange"]! == expectedDeadLetterExchange &&
                (string)args["x-dead-letter-routing-key"]! == expectedQueue),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Verify binding
        rabbitMqClient.Verify(c => c.QueueBindAsync(
            expectedQueue,
            expectedExchange,
            "",
            null,
            false,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    public class ClientNameTestMessage { }

    public class ClientNameTestConsumer : IConsumer<ClientNameTestMessage>
    {
        public Task HandleAsync(ClientNameTestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
