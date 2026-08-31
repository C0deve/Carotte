using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.ClientPrefix;

[Published]
public record ClientPrefixMessage(string Data);

public class ClientPrefixConsumer : IConsumer<ClientPrefixMessage>
{
    public static ClientPrefixMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(ClientPrefixMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class ClientPrefixEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task PublisherAndConsumer_ShouldWorkWithConventionAndClientName()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            ClientPrefixConsumer.LastReceivedMessage = null;
            ClientPrefixConsumer.MessageReceived = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();

            services.AddCarotte(builder =>
            {
                builder.WithClientName("order-service");
                builder.AddBroker("test-broker", options =>
                {
                    options.Host = rabbitMqContainer.Hostname;
                    options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                    options.UserName = RabbitMqBuilder.DefaultUsername;
                    options.Password = RabbitMqBuilder.DefaultPassword;
                });
                builder.AddAssemblies(typeof(ClientPrefixConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.ClientPrefix");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<ClientPrefixMessage>>();
            var messageToSend = new ClientPrefixMessage("ClientPrefixData");

            await publisher.PublishAsync(messageToSend);

            var received = await ClientPrefixConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            ClientPrefixConsumer.LastReceivedMessage.ShouldNotBeNull();
            ClientPrefixConsumer.LastReceivedMessage.Data.ShouldBe("ClientPrefixData");

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
