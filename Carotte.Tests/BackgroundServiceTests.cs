using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Carotte.Tests;

public class BackgroundServiceTests
{
    [Published]
    public class TestMessage;

    [Fact]
    public Task Publisher_ShouldBeInjectableInBackgroundService()
    {
        try
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddCarotte(builder =>
            {
                builder.AddBroker("test-broker", options =>
                {
                    options.Host = "localhost";
                });
                builder.ScanAssemblies(typeof(BackgroundServiceTests).Assembly);
            });

            services.AddSingleton<PublisherUsingBackgroundService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PublisherUsingBackgroundService>());

            var sp = services.BuildServiceProvider();

            // Act
            var backgroundService = sp.GetService<PublisherUsingBackgroundService>();

            // Assert
            backgroundService.ShouldNotBeNull();
            backgroundService.Publisher.ShouldNotBeNull();
            backgroundService.Publisher.ShouldBeAssignableTo<IPublisher<TestMessage>>();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public class PublisherUsingBackgroundService(IPublisher<TestMessage> publisher) : BackgroundService
    {
        public IPublisher<TestMessage> Publisher { get; } = publisher;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
