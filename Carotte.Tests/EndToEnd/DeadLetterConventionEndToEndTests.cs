using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.DeadLetterConvention;

[Published]
public record DeadLetterConventionMessage(string Id, string Content);

public class FailingConventionConsumer : IConsumer<DeadLetterConventionMessage>
{
    public static int AttemptCount { get; set; }

    public Task HandleAsync(DeadLetterConventionMessage message, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        throw new InvalidOperationException("Intentional failure to trigger convention dead lettering.");
    }
}

[Queue("q.dlq.failing-convention-consumer", failureAction: ConsumerFailureAction.Requeue)]
public class ConventionDeadLetterConsumer : IConsumer<DeadLetterConventionMessage>
{
    public static DeadLetterConventionMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(DeadLetterConventionMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class DeadLetterConventionEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task FailingConventionConsumer_ShouldRouteToConventionDeadLetterQueue_WhenProcessingFails()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            FailingConventionConsumer.AttemptCount = 0;
            ConventionDeadLetterConsumer.LastReceivedMessage = null;
            ConventionDeadLetterConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                builder.ScanAssemblies(typeof(FailingConventionConsumer).Assembly)
                    .ScanNamespaces("Carotte.Tests.EndToEnd.DeadLetterConvention");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<DeadLetterConventionMessage>>();
            var messageToSend = new DeadLetterConventionMessage("DLX-CONV-1", "Failed message content");

            await publisher.PublishAsync(messageToSend);

            var received = await ConventionDeadLetterConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));

            received.ShouldBeTrue();
            ConventionDeadLetterConsumer.LastReceivedMessage.ShouldNotBeNull();
            ConventionDeadLetterConsumer.LastReceivedMessage.Id.ShouldBe("DLX-CONV-1");
            ConventionDeadLetterConsumer.LastReceivedMessage.Content.ShouldBe("Failed message content");
            FailingConventionConsumer.AttemptCount.ShouldBeGreaterThan(0);

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
