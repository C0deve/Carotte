using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Carotte.Tests;

public class DIScanTests
{
    [Fact]
    public void AddCarotte_ShouldRegisterConsumer_WhenAttributeIsPresent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => {
            builder.AddAssemblies(typeof(DIScanTests).Assembly);
            builder.ConsumerConfigs[typeof(CarotteTestKitTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(Validation.ValidationTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
        });

        // Assert
        var sp = services.BuildServiceProvider();
        var consumer = sp.GetService<AttributeConsumer>();
        consumer.ShouldNotBeNull();
        
        // Check if Singleton
        var consumero2 = sp.GetService<AttributeConsumer>();
        consumero2.ShouldBeSameAs(consumer);

        // Check if HostedService is registered
        var hostedServices = sp.GetServices<IHostedService>();
        hostedServices.ShouldContain(h => h.GetType().IsGenericType && 
                                            h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                            h.GetType().GetGenericArguments()[0] == typeof(AttributeConsumer));
    }

    [Fact]
    public void AddCarotte_ShouldHandleMultipleInterfaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => {
            builder.AddAssemblies(typeof(DIScanTests).Assembly);
            builder.ConsumerConfigs[typeof(CarotteTestKitTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(Validation.ValidationTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
        });

        // Assert
        var sp = services.BuildServiceProvider();
        var consumer = sp.GetService<MultiConsumer>();
        consumer.ShouldNotBeNull();
        
        var hostedServices = sp.GetServices<IHostedService>();
        hostedServices.ShouldContain(h => h.GetType().IsGenericType && 
                                            h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                            h.GetType().GetGenericArguments()[0] == typeof(MultiConsumer));
    }

    public class Message { }
    public class Message2 { }

    [Queue("test-queue-1", broker: "test-broker")]
    [Queue("test-queue-2", broker: "test-broker")]
    public class MultiConsumer : IConsumer<Message>, IConsumer<Message2>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleAsync(Message2 message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Queue("test-queue", broker: "test-broker")]
    public class AttributeConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
