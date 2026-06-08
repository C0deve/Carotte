using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using Shouldly;

namespace Carotte.Tests;

public class EndToEndTests
{
    [Publisher]
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

    [Fact]
    public async Task PublisherAndConsumer_ShouldWorkWithRealRabbitMQ()
    {
        // 1. Start RabbitMQ container
        var rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4.2.5")
            .WithImage("rabbitmq:4.0-management")
            .Build();

        await rabbitMqContainer.StartAsync();

        try
        {
            var services = new ServiceCollection();
            
            // 2. Configure Carotte
            services.AddCarotte(builder =>
            {
                builder.AddBroker("test-broker", options =>
                {
                    options.Host = rabbitMqContainer.Hostname;
                    options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                    options.UserName = RabbitMqBuilder.DefaultUsername;
                    options.Password = RabbitMqBuilder.DefaultPassword;
                });
                builder.AddAssemblies(typeof(SimpleConsumer).Assembly);
            });

            var serviceProvider = services.BuildServiceProvider();

            // 3. Start BackgroundServices (the consumer)
            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            // Wait a bit for the topology to be created by the consumer
            await Task.Delay(2000);

            // 4. Send a message via the publisher
            var publisher = serviceProvider.GetRequiredService<IPublisher<SimpleMessage>>();
            var messageToSend = new SimpleMessage { Content = "Hello Carotte!" };
            
            await publisher.PublishAsync(messageToSend);

            // 5. Verify reception
            var received = await SimpleConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            
            received.ShouldBeTrue();
            SimpleConsumer.LastReceivedMessage.ShouldNotBeNull();
            SimpleConsumer.LastReceivedMessage.Content.ShouldBe("Hello Carotte!");

            // Stop services
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
