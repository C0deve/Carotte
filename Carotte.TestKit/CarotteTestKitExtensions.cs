using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RabbitMQ.Client;

// ReSharper disable once CheckNamespace
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

            // Remove any previously registered closed generic IPublisher<> descriptors
            var publishers = services.Where(d => d.ServiceType.IsGenericType &&
                                                 d.ServiceType.GetGenericTypeDefinition() == typeof(IPublisher<>))
                .ToList();

            foreach (var pub in publishers)
            {
                services.Remove(pub);
            }

            // Register open generic InMemoryPublisher
            services.AddSingleton(typeof(IPublisher<>), typeof(InMemoryPublisher<>));

            return services;
        }

        public IServiceCollection AddMockPublisher<TMessage>()
        {
            var mock = new Mock<IPublisher<TMessage>>();

            // Register both the Mock and the object for easy retrieval
            services.Replace(ServiceDescriptor.Singleton(mock));
            services.Replace(ServiceDescriptor.Singleton<IPublisher<TMessage>>(sp =>
            {
                var store = sp.GetRequiredService<MessageTestStore>();
                mock.Setup(p => p.PublishAsync(It.IsAny<TMessage>(), It.IsAny<CancellationToken>()))
                    .Callback<TMessage, CancellationToken>((msg, _) => store.Add(msg!))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            }));

            return services;
        }
    }

    extension(IServiceProvider sp)
    {
        public Mock<IPublisher<TMessage>> GetMockPublisher<TMessage>() =>
            sp.GetRequiredService<Mock<IPublisher<TMessage>>>();
    }

    extension(CarotteBuilder builder)
    {
        public CarotteBuilder UseTestKit()
        {
            builder.AddCustomServiceConfigurator(services => services.AddCarotteTestKit());
            return builder;
        }
    }
}
