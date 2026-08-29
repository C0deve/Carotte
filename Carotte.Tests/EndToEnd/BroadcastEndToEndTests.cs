using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.Broadcast;

[Publisher]
public record BroadcastOrderMessage(string OrderId);

public class BroadcastAuditConsumer : IConsumer<BroadcastOrderMessage>
{
    public static BroadcastOrderMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(BroadcastOrderMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class BroadcastNotificationConsumer : IConsumer<BroadcastOrderMessage>
{
    public static BroadcastOrderMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(BroadcastOrderMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class BroadcastEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task PublisherAndMultipleConsumers_ShouldBroadcastWithConventionConfiguration()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            BroadcastAuditConsumer.LastReceivedMessage = null;
            BroadcastAuditConsumer.MessageReceived = new TaskCompletionSource<bool>();
            BroadcastNotificationConsumer.LastReceivedMessage = null;
            BroadcastNotificationConsumer.MessageReceived = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();

            services.AddCarotte(builder =>
            {
                builder.AddBroker("test-broker", options =>
                {
                    options.Host = rabbitMqContainer.Hostname;
                    options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                    options.UserName = RabbitMqBuilder.DefaultUsername;
                    options.Password = RabbitMqBuilder.DefaultPassword;
                });
                builder.AddAssemblies(typeof(BroadcastAuditConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.Broadcast");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<BroadcastOrderMessage>>();
            var orderMessage = new BroadcastOrderMessage("ORDER-12345");

            await publisher.PublishAsync(orderMessage);

            var auditReceived = await BroadcastAuditConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var notificationReceived = await BroadcastNotificationConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            auditReceived.ShouldBeTrue();
            notificationReceived.ShouldBeTrue();
            BroadcastAuditConsumer.LastReceivedMessage.ShouldNotBeNull();
            BroadcastAuditConsumer.LastReceivedMessage.OrderId.ShouldBe("ORDER-12345");
            BroadcastNotificationConsumer.LastReceivedMessage.ShouldNotBeNull();
            BroadcastNotificationConsumer.LastReceivedMessage.OrderId.ShouldBe("ORDER-12345");

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
