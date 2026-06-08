using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RabbitMQ.Client;

namespace Carotte;

public static class CarotteTestKitExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCarotteTestKit()
        {
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

            services.Replace(ServiceDescriptor.Singleton(mockConnectionManager.Object));

            // Replace IRabbitMqClient to avoid any RabbitMQ calls
            var mockRabbitMqClient = new Mock<IRabbitMqClient>();
            services.Replace(ServiceDescriptor.Singleton(mockRabbitMqClient.Object));

            // Replace ITopologyManager to avoid topology declarations
            var mockTopologyManager = new Mock<ITopologyManager>();
            services.Replace(ServiceDescriptor.Singleton(mockTopologyManager.Object));

            // Register a PostConfigure action to replace publishers
            // We need to find the CarotteBuilder in the services to know which publishers to replace
            var builderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CarotteBuilder));
            if (builderDescriptor?.ImplementationInstance is CarotteBuilder builder)
            {
                // Note: We don't have access to PublisherConfigs anymore,
                // so we rely on the fact that IPublisher<T> are registered as singletons.
                // We'll replace them if they are of type RabbitMqPublisher<T>
                var publishers = services.Where(d => d.ServiceType.IsGenericType && 
                                                   d.ServiceType.GetGenericTypeDefinition() == typeof(IPublisher<>))
                                        .ToList();

                foreach (var pub in publishers)
                {
                    var messageType = pub.ServiceType.GetGenericArguments()[0];
                    var implementationType = typeof(InMemoryPublisher<>).MakeGenericType(messageType);

                    services.Replace(ServiceDescriptor.Singleton(pub.ServiceType, sp =>
                    {
                        var store = sp.GetRequiredService<MessageTestStore>();
                        return Activator.CreateInstance(implementationType, store)!;
                    }));
                }
            }

            return services;
        }

        public IServiceCollection AddMockPublisher<TMessage>() where TMessage : class
        {
            var mock = new Mock<IPublisher<TMessage>>();
        
            // Register both the Mock and the object for easy retrieval
            services.Replace(ServiceDescriptor.Singleton(mock));
            services.Replace(ServiceDescriptor.Singleton<IPublisher<TMessage>>(sp => 
            {
                var store = sp.GetRequiredService<MessageTestStore>();
                mock.Setup(p => p.PublishAsync(It.IsAny<TMessage>(), It.IsAny<CancellationToken>()))
                    .Callback<TMessage, CancellationToken>((msg, _) => store.Add(msg))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            }));

            return services;
        }
    }

    public static Mock<IPublisher<TMessage>> GetMockPublisher<TMessage>(this IServiceProvider sp) where TMessage : class => 
        sp.GetRequiredService<Mock<IPublisher<TMessage>>>();
}
