using Microsoft.Extensions.DependencyInjection;

namespace Carotte.Tests;

public class PublisherScanTests
{
    [Publisher(broker: "test-broker", exchange: "scanned-exchange")]
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
        var publisher = serviceProvider.GetService<IPublisher<ScannedMessage>>();
        Assert.NotNull(publisher);
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
                .AddBroker("scanned-broker", _ => { });
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
            carotte.AddAssemblies(typeof(PublisherScanTests).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var publisher = serviceProvider.GetRequiredService<IPublisher<MessageWithDefaultBroker>>();
        Assert.NotNull(publisher);

        // Internal access to verify broker
        var rbPublisher = (RabbitMqPublisher<MessageWithDefaultBroker>)publisher;

        // We find the field that stores the broker name. 
        // In C# 12+ primary constructors, it's typically a private field named <broker>P or just broker.
        var type = typeof(RabbitMqPublisher<MessageWithDefaultBroker>);
        var brokerField = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "broker" || f.Name.Contains("<broker>"));

        Assert.NotNull(brokerField);
        var brokerValue = brokerField.GetValue(rbPublisher);
        Assert.Equal("test-broker", brokerValue);
    }
}