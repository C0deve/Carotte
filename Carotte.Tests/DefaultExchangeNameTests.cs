using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Moq;

namespace Carotte.Tests;

public class DefaultExchangeNameTests
{
    [Fact]
    public void ToDefaultExchangeName_ShouldFormatCorrectly()
    {
        "OrderCreated".ToDefaultExchangeName().ShouldBe("x.pub.order-created");
        "OrderCreatedMessage".ToDefaultExchangeName().ShouldBe("x.pub.order-created");
        "OrderCreatedEvent".ToDefaultExchangeName().ShouldBe("x.pub.order-created");
        "CreateOrderCommand".ToDefaultExchangeName().ShouldBe("x.pub.create-order");
    }

    [Fact]
    public void ConsumerNames_ShouldFormatCorrectly()
    {
        "OrderService".ToConsumerExchangeName().ShouldBe("x.sub.order-service");
        "OrderService".ToConsumerQueueName().ShouldBe("q.order-service");
    }

    [Fact]
    public async Task AddCarotte_ShouldUseFormattedExchangeNameByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        var rabbitMqClient = new Mock<IRabbitMqClient>();
        
        services.AddCarotte(builder => {
            builder
                .AddBroker("test-broker", _ => { })
                .AddAssemblies(typeof(DefaultExchangeNameTests).Assembly);
        });
        
        // We need to replace the IRabbitMqClient to verify calls
        services.AddSingleton(rabbitMqClient.Object);

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();
        var host = hostedServices.FirstOrDefault(h => h.GetType().IsGenericType && 
                                            h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                            h.GetType().GetGenericArguments()[0] == typeof(ExchangeTestConsumer));
        
        host.ShouldNotBeNull();

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        // Expected exchange name for ExchangeTestMessage should be x.pub.exchange-test
        var expectedExchange = "x.pub.exchange-test";
        
        rabbitMqClient.Verify(c => c.ExchangeDeclareAsync(
            expectedExchange,
            "fanout",
            true,
            false,
            null,
            false,
            false,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    public class ExchangeTestMessage { }

    public class ExchangeTestConsumer : IConsumer<ExchangeTestMessage>
    {
        public Task HandleAsync(ExchangeTestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
