using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.ServicePrefix;

[Published]
public record ServicePrefixMessage(string Data);

public class ServicePrefixConsumer : IConsumer<ServicePrefixMessage>
{
    public static ServicePrefixMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(ServicePrefixMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class ServicePrefixEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task PublisherAndConsumer_ShouldWorkWithConventionAndServiceName()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            ServicePrefixConsumer.LastReceivedMessage = null;
            ServicePrefixConsumer.MessageReceived = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();

            services.AddCarotte(builder =>
            {
                builder.WithServiceName("order-service");
                builder.AddBroker("test-broker", options =>
                {
                    options.Host = rabbitMqContainer.Hostname;
                    options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                    options.UserName = RabbitMqBuilder.DefaultUsername;
                    options.Password = RabbitMqBuilder.DefaultPassword;
                });
                builder.ScanAssemblies(typeof(ServicePrefixConsumer).Assembly)
                    .ScanNamespaces("Carotte.Tests.EndToEnd.ServicePrefix");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<ServicePrefixMessage>>();
            var messageToSend = new ServicePrefixMessage("ServicePrefixData");

            await publisher.PublishAsync(messageToSend);

            var received = await ServicePrefixConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            ServicePrefixConsumer.LastReceivedMessage.ShouldNotBeNull();
            ServicePrefixConsumer.LastReceivedMessage.Data.ShouldBe("ServicePrefixData");

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            await rabbitMqContainer.StopAsync();
        }
    }
}
