using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.Convention;

[Published]
public record ConventionMessage(string Content);

public class ConventionConsumer : IConsumer<ConventionMessage>
{
    public static ConventionMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(ConventionMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class ConventionConfigurationEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task PublisherAndConsumer_ShouldWorkWithConventionConfiguration()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            ConventionConsumer.LastReceivedMessage = null;
            ConventionConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                builder.ScanAssemblies(typeof(ConventionConsumer).Assembly)
                    .ScanNamespaces("Carotte.Tests.EndToEnd.Convention");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<ConventionMessage>>();
            var messageToSend = new ConventionMessage("Hello Convention!");

            await publisher.PublishAsync(messageToSend);

            var received = await ConventionConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            ConventionConsumer.LastReceivedMessage.ShouldNotBeNull();
            ConventionConsumer.LastReceivedMessage.Content.ShouldBe("Hello Convention!");

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
