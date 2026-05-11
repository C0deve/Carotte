using Carotte.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Carotte.Tests;

public class ValidationTests
{
    public class Message { }

    public class NoAttributeConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Queue("test-queue", broker: "test-broker")]
    [Queue("test-queue", broker: "test-broker")]
    public class DuplicateQueueConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Queue("queue1", broker: "test-broker")]
    [Queue("queue2", broker: "test-broker")]
    public class MultiQueueConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Queue("test-queue", broker: "test-broker")]
    [Binding("exchange1", "key1")]
    [Binding("exchange2", "key2")]
    public class NewMultiBindingConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Binding("exchange1", "key1")]
    public class BindingWithoutQueueConsumer : IConsumer<Message>
    {
        public Task HandleAsync(Message message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddCarotte_ShouldThrowException_WhenConsumerHasMultipleQueues()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() => {
            services.AddCarotte(builder => {
                builder.AddBroker("test-broker", _ => { });
                builder.ConsumerConfigs[typeof(NoAttributeConsumer)] = ("test-broker", "test-queue");
                builder.AddAssemblies(typeof(ValidationTests).Assembly);
            });
        });

        ex.Message.ShouldContain(nameof(MultiQueueConsumer));
        ex.Message.ShouldContain("can only consume from one queue");
    }

    [Fact]
    public void AddCarotte_ShouldAllowQueueAndBindingAttributes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => {
            builder.AddBroker("test-broker", _ => { });
            builder.ConsumerConfigs[typeof(NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(MultiQueueConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(BindingWithoutQueueConsumer)] = ("test-broker", "test-queue");
            builder.AddAssemblies(typeof(ValidationTests).Assembly);
        });

        // Assert
        // Should not throw, verification of bindings would require deeper inspection or a functional test
    }

    [Fact]
    public void AddCarotte_ShouldAllowBindingWithoutQueue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => {
            builder.AddBroker("test-broker", _ => { });
            builder.AddAssemblies(typeof(ValidationTests).Assembly);
            builder.ConsumerConfigs[typeof(NoAttributeConsumer)] = ("test-broker", "test-queue");
            builder.ConsumerConfigs[typeof(MultiQueueConsumer)] = ("test-broker", "test-queue");
        });

        // Assert
        // Should not throw CarotteConfigurationException for BindingWithoutQueueConsumer
    }

    [Fact]
    public void AddCarotte_ShouldNotThrowException_WhenConsumerHasNoAttribute()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder => {
            builder.AddBroker("test-broker", _ => { });
            builder.AddAssemblies(typeof(ValidationTests).Assembly);
            builder.ConsumerConfigs[typeof(MultiQueueConsumer)] = ("test-broker", "test-queue");
        });

        // Assert
        // Should not throw, NoAttributeConsumer is now automatically configured with its class name as queue.
    }

    [Fact]
    public void AddCarotte_ShouldEmitWarning_WhenConsumerHasDuplicateQueueAttributes()
    {
        // Arrange
        var services = new ServiceCollection();
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            // Act
            services.AddCarotte(builder =>
            {
                builder.AddBroker("test-broker", _ => { });
                // Explicitly configure MultiQueueConsumer to avoid exception due to lack of attribute
                builder.ConsumerConfigs[typeof(MultiQueueConsumer)] = ("test-broker", "test-queue");
                builder.AddAssemblies(typeof(ValidationTests).Assembly);
            });

            // Assert
            var output = sw.ToString();
            output.ShouldContain(nameof(DuplicateQueueConsumer));
            output.ShouldContain("test-queue");
            output.ShouldContain("test-broker");
            output.ShouldContain("warning", Case.Insensitive);
        }
        finally
        {
            Console.SetOut(originalOut);
            sw.Dispose();
        }
    }
    [Fact]
    public void AddCarotte_ShouldThrowException_WhenNoBrokerIsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() => {
            services.AddCarotte(builder => {
                builder.AddAssemblies(typeof(ValidationTests).Assembly);
                // No broker added
            });
        });

        ex.Message.ShouldContain("No broker registered");
    }

    [Fact]
    public void Publisher_ShouldThrowException_WhenNoBrokerIsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCarotte(builder =>
        {
            builder.AddPublisher<Message>();
            // No broker added
        });
        var sp = services.BuildServiceProvider();

        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() =>
        {
            sp.GetRequiredService<IPublisher<Message>>();
        });

        ex.Message.ShouldContain("No broker registered");
    }
}
