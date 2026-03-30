using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Carotte.Tests;

public class BackgroundServiceTests
{
    [Fact]
    public async Task Producer_ShouldBeInjectableInBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddCarotte(builder =>
        {
            builder.AddBroker("test-broker", options =>
            {
                options.Host = "localhost";
            });
            builder.AddProducer<TestMessage>("test-broker", "test-exchange");
        });

        services.AddSingleton<ProducerUsingBackgroundService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ProducerUsingBackgroundService>());

        var sp = services.BuildServiceProvider();

        // Act
        var backgroundService = sp.GetService<ProducerUsingBackgroundService>();
        
        // Assert
        backgroundService.ShouldNotBeNull();
        backgroundService.Producer.ShouldNotBeNull();
        backgroundService.Producer.ShouldBeAssignableTo<IProducer<TestMessage>>();
    }

    public class ProducerUsingBackgroundService : BackgroundService
    {
        public IProducer<TestMessage> Producer { get; }

        public ProducerUsingBackgroundService(IProducer<TestMessage> producer)
        {
            Producer = producer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
