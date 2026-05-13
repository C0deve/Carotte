using Carotte.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Carotte.Tests;

public class ValidationTests
{
    public class Message;

    public class NoAttributeConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Binding("exchange1", "key1")]
    public class BindingWithoutQueueConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddCarotte_ShouldAllowQueueAndBindingAttributes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => builder
            .AddBroker("test-broker", _ => { })
            .AddAssemblies(typeof(ValidationTests).Assembly));

        // Assert
        // Should not throw, verification of bindings would require deeper inspection or a functional test
    }

    [Fact]
    public void AddCarotte_ShouldAllowBindingWithoutQueue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => builder
            .AddBroker("test-broker", _ => { })
            .AddAssemblies(typeof(ValidationTests).Assembly));

        // Assert
        // Should not throw CarotteConfigurationException for BindingWithoutQueueConsumer
    }

    [Fact]
    public void AddCarotte_ShouldNotThrowException_WhenConsumerHasNoAttribute()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => builder
            .AddBroker("test-broker", _ => { })
            .AddAssemblies(typeof(ValidationTests).Assembly));

        // Assert
        // Should not throw, NoAttributeConsumer is now automatically configured with its class name as queue.
    }

    [Fact]
    public void AddCarotte_ShouldThrowException_WhenNoBrokerIsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() =>
        {
            services.AddCarotte(builder =>
            {
                builder.AddAssemblies(typeof(ValidationTests).Assembly);
                // No broker added
            });
        });

        ex.Message.ShouldContain("No broker registered");
    }

    [Fact]
    public void Publisher_ShouldThrowException_WhenNoBrokerIsRegistered()
    {
        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() =>
        {
            new ServiceCollection().AddCarotte(builder =>
                {
                    builder.AddPublisher<Message>();
                    // No broker added
                })
                .BuildServiceProvider()
                .GetRequiredService<IPublisher<Message>>();
        });

        ex.Message.ShouldContain("No broker registered");
    }
}