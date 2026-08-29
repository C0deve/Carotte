using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using System.Reflection;

namespace Carotte.Tests;

public class DefaultQueueNameTests
{
    [Fact]
    public void AddCarotte_ShouldUseFormattedQueueNameByDefault()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(builder =>
        {
            builder
                .AddBroker("test-broker", _ => { })
                .AddAssemblies(typeof(DefaultQueueNameTests).Assembly);
        });

        var sp = services.BuildServiceProvider();

        // Assert
        var hostedServices = sp.GetServices<IHostedService>();
        var host = hostedServices.FirstOrDefault(h => h.GetType().IsGenericType &&
                                            h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                            h.GetType().GetGenericArguments()[0] == typeof(OrderConsumer));

        host.ShouldNotBeNull();

        // Inspect bindings via reflection because they are passed to RabbitMqConsumerHost's constructor
        var field = host.GetType().GetField("topology", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            // Maybe it is prefixed by _ in a compiled version or according to conventions
            field = host.GetType().GetField("_topology", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // If still null, try to find the field that is of type IConsumerTopology
        if (field == null)
        {
            field = host.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => typeof(IConsumerTopology).IsAssignableFrom(f.FieldType));
        }

        field.ShouldNotBeNull();
        var topology = (IConsumerTopology)field.GetValue(host)!;

        topology.Queue.ShouldBe("q.order-consumer");
    }

    public class OrderMessage { }

    // Without Queue attribute, should use the formatted default name
    public class OrderConsumer : IConsumer<OrderMessage>
    {
        public Task HandleAsync(OrderMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
