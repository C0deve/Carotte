using System.Reflection;
using JetBrains.Annotations;
using Shouldly;

namespace Carotte.Tests;

[TestSubject(typeof(CarotteScanner))]
public class CarotteScannerTest
{
    [Fact]
    public void Scan_ShouldFindConsumersAndPublishers()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly };

        // Act
        var (consumers, publishers) = assemblies.Scan();

        // Assert
        consumers.ShouldNotBeEmpty();
        consumers.Any(c => c.ConsumerType == typeof(TestConsumer)).ShouldBeTrue();

        var testConsumer = consumers.Single(c => c.ConsumerType == typeof(TestConsumer));
        testConsumer.MessageTypes.ShouldContain(typeof(TestMessage));
        testConsumer.QueueAttr.ShouldNotBeNull();
        testConsumer.QueueAttr.Name.ShouldBe("test-queue");
        testConsumer.BindingAttrs.Count.ShouldBe(1);
        testConsumer.BindingAttrs[0].Exchange.ShouldBe("test-exchange");

        publishers.ShouldNotBeEmpty();
        publishers
            .Select(result => result.MessageType)
            .ShouldContain(typeof(TestPublishedMessage));

        var testPublisher = publishers.Single(p => p.MessageType == typeof(TestPublishedMessage));
        testPublisher.PublisherAttribute.Exchange.ShouldBe("pub-exchange");
    }

    [Fact]
    public void Scan_ShouldHandleMultipleInterfaces()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly };

        // Act
        var (consumers, _) = assemblies.Scan();

        // Assert
        var multiConsumer = consumers.FirstOrDefault(c => c.ConsumerType == typeof(MultiMessageConsumer));
        multiConsumer.ConsumerType.ShouldNotBeNull();
        multiConsumer.MessageTypes.ShouldContain(typeof(TestMessage));
        multiConsumer.MessageTypes.ShouldContain(typeof(OtherMessage));
    }

    public class TestMessage;

    public class OtherMessage;

    [Queue("test-queue")]
    [Binding("test-exchange", "test-routing-key")]
    public class TestConsumer : IConsumer<TestMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MultiMessageConsumer : IConsumer<TestMessage>, IConsumer<OtherMessage>
    {
        public Task HandleAsync(TestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleAsync(OtherMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void Scan_ShouldIgnoreAbstractClasses()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly };

        // Act
        var (consumers, _) = assemblies.Scan();

        // Assert
        consumers
            .Select(result => result.ConsumerType)
            .ShouldNotContain(typeof(AbstractConsumer));
    }

    [Fact]
    public void Scan_ShouldHandleEmptyAssemblies()
    {
        // Arrange
        var assemblies = new HashSet<Assembly>();

        // Act
        var (consumers, publishers) = assemblies.Scan();

        // Assert
        consumers.ShouldBeEmpty();
        publishers.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_ShouldFilterByNamespace()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly };
        var namespaces = new List<string> { "Carotte.Tests" };

        // Act
        var (consumers, _) = assemblies.Scan(namespaces);

        // Assert
        consumers.ShouldNotBeEmpty();
        consumers.All(c => c.ConsumerType.Namespace != null && c.ConsumerType.Namespace.StartsWith("Carotte.Tests")).ShouldBeTrue();
    }

    [Fact]
    public void Scan_ShouldExcludeTypesNotInNamespace()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly };
        var namespaces = new List<string> { "NonExistentNamespace" };

        // Act
        var (consumers, publishers) = assemblies.Scan(namespaces);

        // Assert
        consumers.ShouldBeEmpty();
        publishers.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_ShouldHandleMultipleNamespaces()
    {
        // Arrange
        var assemblies = new HashSet<Assembly> { typeof(CarotteScannerTest).Assembly, typeof(CarotteScanner).Assembly };
        var namespaces = new List<string> { "Carotte.Tests", "Carotte" };

        // Act
        var (consumers, _) = assemblies.Scan(namespaces);

        // Assert
        consumers.ShouldNotBeEmpty();
        consumers.Any(c => c.ConsumerType.Namespace == "Carotte.Tests").ShouldBeTrue();
        // Since we don't have consumers in "Carotte" namespace in the core lib (usually), 
        // we just verify that it doesn't crash and finds what's in the other namespace.
    }

    public abstract class AbstractConsumer : IConsumer<TestMessage>
    {
        public abstract Task HandleAsync(TestMessage message, CancellationToken cancellationToken);
    }

    [Publisher(exchange: "pub-exchange")]
    public class TestPublishedMessage
    {
    }
}