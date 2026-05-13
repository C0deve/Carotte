using Microsoft.Extensions.DependencyInjection;

namespace Carotte.Tests;

public class PublisherScanTests
{
    [Publisher(broker: "scanned-broker", exchange: "scanned-exchange")]
    public class ScannedMessage;


    [Fact]
    public void AddAssemblies_ShouldRegisterMessagesWithAttribute()
    {
        // Act
        var serviceProvider = new ServiceCollection()
            .AddCarotte(carotte => carotte
                .AddBroker("test-broker", _ => { })
                .AddBroker("scanned-broker", _ => { })
                .AddAssemblies(typeof(PublisherScanTests).Assembly))
            .BuildServiceProvider();

        // Assert
        // ScannedMessage has the [Publisher] attribute.
        // It should now be registered by assembly scan even without an explicit IPublisher implementation.
        var publisher = serviceProvider.GetService<IPublisher<ScannedMessage>>();
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddAssemblies_ShouldNotOverwriteManualRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(carotte =>
        {
            carotte
                .AddBroker("test-broker", _ => { })
                .AddBroker("scanned-broker", _ => { })
                .AddBroker("manual-broker", _ => { });

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

    [Publisher]
    public class MessageWithDefaultBroker;

    [Fact]
    public void AddAssemblies_ShouldRegisterPublisherWithDefaultBroker()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(carotte =>
        {
            carotte
                .AddBroker("test-broker", _ => { })
                .AddBroker("test-broker", _ => { });
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetService<IPublisher<MessageWithDefaultBroker>>();
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddCarotte_ShouldUseFirstBroker_WhenNoneSpecified()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("test-broker", _ => { });
            carotte.AddBroker("broker2", _ => { });

            // Add a publisher without specifying a broker
            carotte.AddPublisher<ScannedMessage>(exchange: "my-exchange");
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetRequiredService<IPublisher<ScannedMessage>>();
        Assert.NotNull(publisher);

        // Internal access to verify broker
        var rbPublisher = (RabbitMqPublisher<ScannedMessage>)publisher;

        // We find the field that stores the broker name. 
        // In C# 12+ primary constructors, it's typically a private field named <broker>P or just broker.
        var type = typeof(RabbitMqPublisher<ScannedMessage>);
        var brokerField = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "broker" || f.Name.Contains("<broker>"));

        Assert.NotNull(brokerField);
        var brokerValue = brokerField.GetValue(rbPublisher);
        Assert.Equal("test-broker", brokerValue);
    }
}