using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.Explicit;

[Published(exchange: "simple-exchange")]
public class SimpleMessage
{
    public string Content { get; set; } = string.Empty;
}

[Queue("simple-queue", exchange: "simple-exchange")]
public class SimpleConsumer : IConsumer<SimpleMessage>
{
    public static SimpleMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(SimpleMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class ExplicitConfigurationEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task PublisherAndConsumer_ShouldWorkWithRealRabbitMQ()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            SimpleConsumer.LastReceivedMessage = null;
            SimpleConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                builder.AddAssemblies(typeof(SimpleConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.Explicit");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<SimpleMessage>>();
            var messageToSend = new SimpleMessage { Content = "Hello Carotte!" };

            await publisher.PublishAsync(messageToSend);

            var received = await SimpleConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            SimpleConsumer.LastReceivedMessage.ShouldNotBeNull();
            SimpleConsumer.LastReceivedMessage.Content.ShouldBe("Hello Carotte!");

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
