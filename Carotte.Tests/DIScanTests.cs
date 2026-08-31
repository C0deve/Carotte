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
        services.AddCarotte(builder => builder
            .AddBroker("test-broker", _ => { })
            .ScanAssemblies(typeof(DIScanTests).Assembly));

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
        var services = new ServiceCollection()
            .AddCarotte(builder => builder
                .AddBroker("test-broker", _ => { })
                .ScanAssemblies(typeof(DIScanTests).Assembly));

        // Assert
        var sp = services.BuildServiceProvider();
        var consumer = sp.GetService<MultiConsumer>();
        consumer.ShouldNotBeNull();

        var hostedServices = sp.GetServices<IHostedService>();
        hostedServices.ShouldContain(h => h.GetType().IsGenericType &&
                                          h.GetType().GetGenericTypeDefinition() == typeof(RabbitMqConsumerHost<>) &&
                                          h.GetType().GetGenericArguments()[0] == typeof(MultiConsumer));
    }

    [Fact]
    public void CarotteBuilder_ScanAssembly_ShouldAddAssembly()
    {
        var builder = new CarotteBuilder();
        builder.ScanAssembly(typeof(DIScanTests).Assembly);
        builder.Assemblies.ShouldContain(typeof(DIScanTests).Assembly);
    }

    [Fact]
    public void CarotteBuilder_ScanAssemblyContaining_ShouldAddAssembly()
    {
        var builder = new CarotteBuilder();
        builder.ScanAssemblyContaining<DIScanTests>();
        builder.Assemblies.ShouldContain(typeof(DIScanTests).Assembly);
    }

    [Fact]
    public void CarotteBuilder_ScanNamespace_ShouldAddNamespace()
    {
        var builder = new CarotteBuilder();
        builder.ScanNamespace("Carotte.Tests");
        builder.Namespaces.ShouldContain("Carotte.Tests");
    }

    [Fact]
    public void CarotteBuilder_ScanNamespaceOf_ShouldAddNamespace()
    {
        var builder = new CarotteBuilder();
        builder.ScanNamespaceOf<DIScanTests>();
        builder.Namespaces.ShouldContain("Carotte.Tests");
    }

    [Fact]
    public void CarotteBuilder_ScanNamespaces_ShouldAddMultipleNamespaces()
    {
        var builder = new CarotteBuilder();
        builder.ScanNamespaces("Carotte.Tests.A", "Carotte.Tests.B");
        builder.Namespaces.ShouldContain("Carotte.Tests.A");
        builder.Namespaces.ShouldContain("Carotte.Tests.B");
    }

    public class Message
    {
    }

    public class Message2
    {
    }

    [Queue("test-queue-1", broker: "test-broker")]
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