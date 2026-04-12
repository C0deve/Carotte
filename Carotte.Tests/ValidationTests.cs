using Carotte.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Carotte.Tests.Validation;

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

    [Fact]
    public void AddCarotte_ShouldThrowException_WhenConsumerHasNoQueueAttribute()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<CarotteConfigurationException>(() => {
            services.AddCarotte(builder => {
                builder.AddAssemblies(typeof(ValidationTests).Assembly);
            });
        });

        ex.Message.ShouldContain(nameof(NoAttributeConsumer));
        ex.Message.ShouldContain("QueueAttribute");
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
                // On configure explicitement les NoAttributeConsumer pour éviter l'exception d'absence d'attribut
                builder.ConsumerConfigs[typeof(NoAttributeConsumer)] = ("test-broker", "test-queue");
                builder.ConsumerConfigs[typeof(CarotteTestKitTests.NoAttributeConsumer)] = ("test-broker", "test-queue");
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
}
