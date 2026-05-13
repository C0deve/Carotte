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
        "OrderCreated".ToDefaultExchangeName().ShouldBe("message-order-created");
        "OrderCreatedMessage".ToDefaultExchangeName().ShouldBe("message-order-created");
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
            // Bypass other consumers
            builder.ConsumerConfigs[typeof(CarotteTestKitTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(ValidationTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(ValidationTests.BindingWithoutQueueConsumer)] = ("test-broker", "test-queue");
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
        // Expected exchange name for ExchangeTestMessage should be message-exchange-test
        var expectedExchange = "message-exchange-test";
        
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
