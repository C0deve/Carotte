using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Moq;

namespace Carotte.Tests;

public class ServiceNameTests
{
    [Fact]
    public void Extensions_WithServiceName_ShouldIncludePrefix()
    {
        "MyConsumer".ToConsumerExchangeName("MyService").ShouldBe("x.sub.my-service.my-consumer");
        "MyConsumer".ToConsumerQueueName("MyService").ShouldBe("q.my-service.my-consumer");
    }

    [Fact]
    public void Extensions_WithoutServiceName_ShouldNotIncludePrefix()
    {
        "MyConsumer".ToConsumerExchangeName().ShouldBe("x.sub.my-consumer");
        "MyConsumer".ToConsumerQueueName().ShouldBe("q.my-consumer");
    }

    [Fact]
    public async Task AddCarotte_WithServiceName_ShouldUsePrefixedNames()
    {
        // Arrange
        var services = new ServiceCollection();
        var rabbitMqClient = new Mock<IRabbitMqClient>();

        services.AddCarotte(builder =>
        {
            builder
                .WithServiceName("OrderApi")
                .AddBroker("test-broker", _ => { })
                .ScanAssemblies(typeof(ServiceNameTests).Assembly)
                .ScanNamespaces("Carotte.Tests");
        });

        services.AddSingleton(rabbitMqClient.Object);

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();
        var host = hostedServices.FirstOrDefault(h => h.GetType().IsGenericType &&
                                                      h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                                      h.GetType().GetGenericArguments()[0] == typeof(ServiceNameTestConsumer));

        host.ShouldNotBeNull();

        // Act
        await host.StartAsync(CancellationToken.None);

        // Assert
        const string expectedExchange = "x.sub.order-api.service-name-test-consumer";
        const string expectedQueue = "q.order-api.service-name-test-consumer";
        const string expectedDeadLetterExchange = "x.dlx.order-api.service-name-test-consumer";

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

    [Fact]
    public void WithServiceName_ExplicitString_ShouldSetServiceName() =>
        new CarotteBuilder()
            .WithServiceName("my-service")
            .ServiceName
            .ShouldBe("my-service");

    [Fact]
    public void WithServiceNameFrom_GenericType_ShouldSetServiceNameFromAssembly() =>
        new CarotteBuilder()
            .WithServiceNameFrom<ServiceNameTests>()
            .ServiceName
            .ShouldBe(typeof(ServiceNameTests).Assembly.GetName().Name);

    [Fact]
    public void WithServiceNameFrom_Assembly_ShouldSetServiceNameFromAssembly() =>
        new CarotteBuilder()
            .WithServiceNameFrom(typeof(ServiceNameTests).Assembly)
            .ServiceName
            .ShouldBe(typeof(ServiceNameTests).Assembly.GetName().Name);

    [Fact]
    public void WithServiceNameFromEntryAssembly_ShouldSetServiceName() =>
        new CarotteBuilder()
            .WithServiceNameFromEntryAssembly()
            .ServiceName
            .ShouldNotBeNullOrWhiteSpace();

    public class ServiceNameTestMessage
    {
    }

    public class ServiceNameTestConsumer : IConsumer<ServiceNameTestMessage>
    {
        public Task HandleAsync(ServiceNameTestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
