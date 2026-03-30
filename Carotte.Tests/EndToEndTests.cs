using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using Shouldly;

namespace Carotte.Tests;

public class EndToEndTests
{
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
    public async Task ProducerAndConsumer_ShouldWorkWithRealRabbitMQ()
    {
        // 1. Démarrer le conteneur RabbitMQ
        var rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4.2.5")
            .WithImage("rabbitmq:4.0-management")
            .Build();

        await rabbitMqContainer.StartAsync();

        try
        {
            var services = new ServiceCollection();
            
            // 2. Configurer Carotte
            services.AddCarotte(builder =>
            {
                builder.AddBroker("default", options =>
                {
                    options.Host = rabbitMqContainer.Hostname;
                    options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                    options.UserName = RabbitMqBuilder.DefaultUsername;
                    options.Password = RabbitMqBuilder.DefaultPassword;
                });
                builder.AddProducer<SimpleMessage>("default", "simple-exchange");
                builder.AddAssemblies(typeof(SimpleConsumer).Assembly);
            });

            var serviceProvider = services.BuildServiceProvider();

            // 3. Démarrer les BackgroundServices (le consommateur)
            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            // Attendre un peu que la topologie soit créée par le consommateur
            await Task.Delay(2000);

            // 4. Envoyer un message via le producteur
            var producer = serviceProvider.GetRequiredService<IProducer<SimpleMessage>>();
            var messageToSend = new SimpleMessage { Content = "Hello Carotte!" };
            
            await producer.SendAsync(messageToSend);

            // 5. Vérifier la réception
            var received = await SimpleConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            
            received.ShouldBeTrue();
            SimpleConsumer.LastReceivedMessage.ShouldNotBeNull();
            SimpleConsumer.LastReceivedMessage.Content.ShouldBe("Hello Carotte!");

            // Arrêter les services
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
