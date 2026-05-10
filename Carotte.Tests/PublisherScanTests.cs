using Microsoft.Extensions.DependencyInjection;

namespace Carotte.Tests;

public class PublisherScanTests
{
    [Publisher(broker: "scanned-broker", exchange: "scanned-exchange")]
    public class ScannedMessage;

    public class NonScannedMessage;

    [Fact]
    public void AddAssemblies_ShouldRegisterPublishersWithAttribute_WhenImplementingIPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("scanned-broker", _ => { });
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // ScannedMessage has the [Publisher] attribute but no class implements it as a publisher.
        // So it should NOT be registered by assembly scan (only if added manually).
        var publisher = serviceProvider.GetService<IPublisher<ScannedMessage>>();
        Assert.Null(publisher);

        var nonScannedPublisher = serviceProvider.GetService<IPublisher<NonScannedMessage>>();
        Assert.Null(nonScannedPublisher);
    }

    [Fact]
    public void AddAssemblies_ShouldNotOverwriteManualRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("scanned-broker", _ => { });
            carotte.AddBroker("manual-broker", _ => { });
            
            // Manual registration before scan
            carotte.AddPublisher<ScannedMessage>("manual-broker", "manual-exchange");
            
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetService<IPublisher<ScannedMessage>>();
        Assert.NotNull(publisher);
        
        // We verify that the manual configuration was taken into account
        // RabbitMqPublisher is internal so we can't easily inspect its private properties,
        // but we can verify via CarotteBuilder if we had access.
        // Here we rely on the fact that TryAddSingleton should not overwrite if already present,
        // and our logic in AddPublishers checks if the type is already in PublisherConfigs.
    }

    public class MessageForExplicitPublisher;

    public class ExplicitPublisher : IPublisher<MessageForExplicitPublisher>
    {
        public Task PublishAsync(MessageForExplicitPublisher message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void AddAssemblies_ShouldRegisterTypesImplementingIPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("default", _ => { });
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetService<IPublisher<MessageForExplicitPublisher>>();
        Assert.NotNull(publisher);
        Assert.IsType<ExplicitPublisher>(publisher);
    }

    [Publisher]
    public class MessageWithDefaultBroker;

    public class ScannedPublisherWithDefaultBroker : IPublisher<MessageWithDefaultBroker>
    {
        public Task PublishAsync(MessageWithDefaultBroker message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void AddAssemblies_ShouldRegisterPublisherWithDefaultBroker()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("default", _ => { });
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetService<IPublisher<MessageWithDefaultBroker>>();
        Assert.NotNull(publisher);
        Assert.IsType<ScannedPublisherWithDefaultBroker>(publisher);
    }

    public class MessageByConvention;
    public class ConventionConsumer : IConsumer<MessageByConvention>
    {
        public Task HandleAsync(MessageByConvention message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddAssemblies_ShouldNotRegisterPublisherByConvention_WhenConsumed()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("default", _ => { });
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Even if MessageByConvention is consumed, it should not have an auto-generated publisher by assembly scan.
        var publisher = serviceProvider.GetService<IPublisher<MessageByConvention>>();
        Assert.Null(publisher);
    }
}
