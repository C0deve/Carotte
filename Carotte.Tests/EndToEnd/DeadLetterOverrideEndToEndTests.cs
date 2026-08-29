using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.DeadLetterOverride;

[Publisher]
public record DeadLetterOverrideMessage(string Key, string Value);

public class FailingOverrideConsumer : IConsumer<DeadLetterOverrideMessage>
{
    public static int AttemptCount { get; set; }

    public Task HandleAsync(DeadLetterOverrideMessage message, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        throw new InvalidOperationException("Intentional failure to trigger programmatic override dead lettering.");
    }
}

[Queue("configured-override-dlq", failureAction: ConsumerFailureAction.Requeue)]
public class OverrideDeadLetterConsumer : IConsumer<DeadLetterOverrideMessage>
{
    public static DeadLetterOverrideMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(DeadLetterOverrideMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class DeadLetterOverrideEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task FailingOverrideConsumer_ShouldRouteToConfiguredDeadLetterQueue_WhenProcessingFails()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            FailingOverrideConsumer.AttemptCount = 0;
            OverrideDeadLetterConsumer.LastReceivedMessage = null;
            OverrideDeadLetterConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                builder.AddAssemblies(typeof(FailingOverrideConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.DeadLetterOverride");

                builder.ConfigureConsumer("FailingOverrideConsumer", options =>
                {
                    options.MaxRetryAttempts = 0;
                    options.DeadLetterExchange = "configured-override-dlx";
                    options.DeadLetterQueue = "configured-override-dlq";
                    options.DeadLetterRoutingKey = "override.key";
                });
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<DeadLetterOverrideMessage>>();
            var messageToSend = new DeadLetterOverrideMessage("MyKey", "MyValue");

            await publisher.PublishAsync(messageToSend);

            var received = await OverrideDeadLetterConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            OverrideDeadLetterConsumer.LastReceivedMessage.ShouldNotBeNull();
            OverrideDeadLetterConsumer.LastReceivedMessage.Key.ShouldBe("MyKey");
            OverrideDeadLetterConsumer.LastReceivedMessage.Value.ShouldBe("MyValue");
            FailingOverrideConsumer.AttemptCount.ShouldBe(1);

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
