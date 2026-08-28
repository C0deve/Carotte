using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.DeadLetterExplicit;

[Publisher(exchange: "explicit-dl-exchange", routingKey: "order.create")]
public record DeadLetterExplicitMessage(string OrderId, decimal Amount);

[Queue(
    name: "explicit-processing-queue",
    exchange: "explicit-dl-exchange",
    routingKey: "order.create",
    deadLetterExchange: "custom-orders-dlx",
    deadLetterQueue: "custom-orders-dlq",
    deadLetterRoutingKey: "order.dlq",
    maxRetryAttempts: 0)]
public class FailingExplicitConsumer : IConsumer<DeadLetterExplicitMessage>
{
    public static int AttemptCount { get; set; }

    public Task HandleAsync(DeadLetterExplicitMessage message, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        throw new InvalidOperationException("Intentional failure to trigger explicit dead lettering.");
    }
}

[Queue("custom-orders-dlq")]
public class ExplicitDeadLetterConsumer : IConsumer<DeadLetterExplicitMessage>
{
    public static DeadLetterExplicitMessage? LastReceivedMessage { get; set; }
    public static TaskCompletionSource<bool> MessageReceived { get; set; } = new();

    public Task HandleAsync(DeadLetterExplicitMessage message, CancellationToken cancellationToken = default)
    {
        LastReceivedMessage = message;
        MessageReceived.TrySetResult(true);
        return Task.CompletedTask;
    }
}

public class DeadLetterExplicitEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task FailingExplicitConsumer_ShouldRouteToCustomDeadLetterQueue_WhenProcessingFails()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            FailingExplicitConsumer.AttemptCount = 0;
            ExplicitDeadLetterConsumer.LastReceivedMessage = null;
            ExplicitDeadLetterConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                builder.AddAssemblies(typeof(FailingExplicitConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.DeadLetterExplicit");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var publisher = serviceProvider.GetRequiredService<IPublisher<DeadLetterExplicitMessage>>();
            var messageToSend = new DeadLetterExplicitMessage("ORDER-DLX-99", 149.99m);

            await publisher.PublishAsync(messageToSend);

            var received = await ExplicitDeadLetterConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            received.ShouldBeTrue();
            ExplicitDeadLetterConsumer.LastReceivedMessage.ShouldNotBeNull();
            ExplicitDeadLetterConsumer.LastReceivedMessage.OrderId.ShouldBe("ORDER-DLX-99");
            ExplicitDeadLetterConsumer.LastReceivedMessage.Amount.ShouldBe(149.99m);
            FailingExplicitConsumer.AttemptCount.ShouldBe(1);

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
