using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RabbitMQ.Client;

namespace Carotte;

public static class CarotteTestKitExtensions
{
    public static CarotteBuilder UseTestMode(this CarotteBuilder builder)
    {
        var services = builder.Services;

        // Register core TestKit components
        services.AddSingleton<MessageTestStore>();
        services.AddSingleton<CarotteTestKit>();

        // Replace IConnectionManager with a mock that does nothing
        // to avoid real connection attempts to RabbitMQ
        var mockConnectionManager = new Mock<IConnectionManager>();
        var mockConnection = new Mock<IConnection>();
        var mockChannel = new Mock<IChannel>();

        mockConnectionManager.Setup(m => m.GetConnectionAsync(It.IsAny<string>()))
            .ReturnsAsync(mockConnection.Object);
        mockConnection.Setup(m => m.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockChannel.Object);

        services.AddSingleton(mockConnectionManager.Object);
        services.AddSingleton(mockConnectionManager.Object);

        // Replace ITopologyManager to avoid topology declarations
        var mockTopologyManager = new Mock<ITopologyManager>();
        services.AddSingleton(mockTopologyManager.Object);
        services.AddSingleton(mockTopologyManager.Object);

        // Use PostConfigureActions to register InMemory producers AFTER all AddProducer calls have been made
        builder.PostConfigureActions.Add(s => 
        {
            foreach (var prodConfig in builder.ProducerConfigs)
            {
                var interfaceType = typeof(IProducer<>).MakeGenericType(prodConfig.MessageType);
                var implementationType = typeof(InMemoryProducer<>).MakeGenericType(prodConfig.MessageType);
                
                s.Replace(ServiceDescriptor.Singleton(interfaceType, sp => 
                {
                    var store = sp.GetRequiredService<MessageTestStore>();
                    return Activator.CreateInstance(implementationType, store)!;
                }));
            }
        });

        return builder;
    }

    public static IServiceCollection AddMockProducer<TMessage>(this IServiceCollection services) where TMessage : class
    {
        var mock = new Mock<IProducer<TMessage>>();
        
        // Register both the Mock and the object for easy retrieval
        services.Replace(ServiceDescriptor.Singleton(mock));
        services.Replace(ServiceDescriptor.Singleton<IProducer<TMessage>>(sp => 
        {
            var store = sp.GetRequiredService<MessageTestStore>();
            mock.Setup(p => p.SendAsync(It.IsAny<TMessage>(), It.IsAny<CancellationToken>()))
                .Callback<TMessage, CancellationToken>((msg, _) => store.Add(msg))
                .Returns(Task.CompletedTask);
            return mock.Object;
        }));

        return services;
    }

    public static Mock<IProducer<TMessage>> GetMockProducer<TMessage>(this IServiceProvider sp) where TMessage : class => 
        sp.GetRequiredService<Mock<IProducer<TMessage>>>();
}
