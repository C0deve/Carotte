using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.Explicit
{
    [Publisher(exchange: "simple-exchange")]
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
}

namespace Carotte.Tests.EndToEnd.Convention
{
    [Publisher]
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
}

namespace Carotte.Tests.EndToEnd.Broadcast
{
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
}

namespace Carotte.Tests.EndToEnd.ClientPrefix
{
    [Publisher]
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
}

namespace Carotte.Tests.EndToEnd.MultiMessage
{
    [Publisher]
    public record ItemCreatedMessage(Guid ItemId, string Name);

    [Publisher]
    public record ItemUpdatedMessage(Guid ItemId, string NewName);

    public class MultiMessageConventionConsumer : IConsumer<ItemCreatedMessage>, IConsumer<ItemUpdatedMessage>
    {
        public static ItemCreatedMessage? LastCreatedMessage { get; set; }
        public static ItemUpdatedMessage? LastUpdatedMessage { get; set; }
        public static TaskCompletionSource<bool> CreatedReceived { get; set; } = new();
        public static TaskCompletionSource<bool> UpdatedReceived { get; set; } = new();

        public Task HandleAsync(ItemCreatedMessage message, CancellationToken cancellationToken = default)
        {
            LastCreatedMessage = message;
            CreatedReceived.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task HandleAsync(ItemUpdatedMessage message, CancellationToken cancellationToken = default)
        {
            LastUpdatedMessage = message;
            UpdatedReceived.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}

namespace Carotte.Tests
{
    public class EndToEndTests
    {
        private static RabbitMqContainer CreateContainer()
        {
            return new RabbitMqBuilder("rabbitmq:4.2.5")
                .WithImage("rabbitmq:4.0-management")
                .Build();
        }

        [Fact]
        public async Task PublisherAndConsumer_ShouldWorkWithRealRabbitMQ()
        {
            var rabbitMqContainer = CreateContainer();
            await rabbitMqContainer.StartAsync();

            try
            {
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
                    builder.AddAssemblies(typeof(EndToEnd.Explicit.SimpleConsumer).Assembly)
                        .AddNamespaces("Carotte.Tests.EndToEnd.Explicit");
                });

                var serviceProvider = services.BuildServiceProvider();

                foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                await Task.Delay(2000);

                var publisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.Explicit.SimpleMessage>>();
                var messageToSend = new EndToEnd.Explicit.SimpleMessage { Content = "Hello Carotte!" };

                await publisher.PublishAsync(messageToSend);

                var received = await EndToEnd.Explicit.SimpleConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                received.ShouldBeTrue();
                EndToEnd.Explicit.SimpleConsumer.LastReceivedMessage.ShouldNotBeNull();
                EndToEnd.Explicit.SimpleConsumer.LastReceivedMessage.Content.ShouldBe("Hello Carotte!");

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

        [Fact]
        public async Task PublisherAndConsumer_ShouldWorkWithConventionConfiguration()
        {
            var rabbitMqContainer = CreateContainer();
            await rabbitMqContainer.StartAsync();

            try
            {
                EndToEnd.Convention.ConventionConsumer.LastReceivedMessage = null;
                EndToEnd.Convention.ConventionConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                    builder.AddAssemblies(typeof(EndToEnd.Convention.ConventionConsumer).Assembly)
                        .AddNamespaces("Carotte.Tests.EndToEnd.Convention");
                });

                var serviceProvider = services.BuildServiceProvider();

                foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                await Task.Delay(2000);

                var publisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.Convention.ConventionMessage>>();
                var messageToSend = new EndToEnd.Convention.ConventionMessage("Hello Convention!");

                await publisher.PublishAsync(messageToSend);

                var received = await EndToEnd.Convention.ConventionConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                received.ShouldBeTrue();
                EndToEnd.Convention.ConventionConsumer.LastReceivedMessage.ShouldNotBeNull();
                EndToEnd.Convention.ConventionConsumer.LastReceivedMessage.Content.ShouldBe("Hello Convention!");

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

        [Fact]
        public async Task PublisherAndMultipleConsumers_ShouldBroadcastWithConventionConfiguration()
        {
            var rabbitMqContainer = CreateContainer();
            await rabbitMqContainer.StartAsync();

            try
            {
                EndToEnd.Broadcast.BroadcastAuditConsumer.LastReceivedMessage = null;
                EndToEnd.Broadcast.BroadcastAuditConsumer.MessageReceived = new TaskCompletionSource<bool>();
                EndToEnd.Broadcast.BroadcastNotificationConsumer.LastReceivedMessage = null;
                EndToEnd.Broadcast.BroadcastNotificationConsumer.MessageReceived = new TaskCompletionSource<bool>();

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
                    builder.AddAssemblies(typeof(EndToEnd.Broadcast.BroadcastAuditConsumer).Assembly)
                        .AddNamespaces("Carotte.Tests.EndToEnd.Broadcast");
                });

                var serviceProvider = services.BuildServiceProvider();

                foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                await Task.Delay(2000);

                var publisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.Broadcast.BroadcastOrderMessage>>();
                var orderMessage = new EndToEnd.Broadcast.BroadcastOrderMessage("ORDER-12345");

                await publisher.PublishAsync(orderMessage);

                var auditReceived = await EndToEnd.Broadcast.BroadcastAuditConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var notificationReceived = await EndToEnd.Broadcast.BroadcastNotificationConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                auditReceived.ShouldBeTrue();
                notificationReceived.ShouldBeTrue();
                EndToEnd.Broadcast.BroadcastAuditConsumer.LastReceivedMessage.ShouldNotBeNull();
                EndToEnd.Broadcast.BroadcastAuditConsumer.LastReceivedMessage.OrderId.ShouldBe("ORDER-12345");
                EndToEnd.Broadcast.BroadcastNotificationConsumer.LastReceivedMessage.ShouldNotBeNull();
                EndToEnd.Broadcast.BroadcastNotificationConsumer.LastReceivedMessage.OrderId.ShouldBe("ORDER-12345");

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

        [Fact]
        public async Task PublisherAndConsumer_ShouldWorkWithConventionAndClientName()
        {
            var rabbitMqContainer = CreateContainer();
            await rabbitMqContainer.StartAsync();

            try
            {
                EndToEnd.ClientPrefix.ClientPrefixConsumer.LastReceivedMessage = null;
                EndToEnd.ClientPrefix.ClientPrefixConsumer.MessageReceived = new TaskCompletionSource<bool>();

                var services = new ServiceCollection();

                services.AddCarotte(builder =>
                {
                    builder.SetClientName("order-service");
                    builder.AddBroker("test-broker", options =>
                    {
                        options.Host = rabbitMqContainer.Hostname;
                        options.Port = rabbitMqContainer.GetMappedPublicPort(5672);
                        options.UserName = RabbitMqBuilder.DefaultUsername;
                        options.Password = RabbitMqBuilder.DefaultPassword;
                    });
                    builder.AddAssemblies(typeof(EndToEnd.ClientPrefix.ClientPrefixConsumer).Assembly)
                        .AddNamespaces("Carotte.Tests.EndToEnd.ClientPrefix");
                });

                var serviceProvider = services.BuildServiceProvider();

                foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                await Task.Delay(2000);

                var publisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.ClientPrefix.ClientPrefixMessage>>();
                var messageToSend = new EndToEnd.ClientPrefix.ClientPrefixMessage("ClientPrefixData");

                await publisher.PublishAsync(messageToSend);

                var received = await EndToEnd.ClientPrefix.ClientPrefixConsumer.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                received.ShouldBeTrue();
                EndToEnd.ClientPrefix.ClientPrefixConsumer.LastReceivedMessage.ShouldNotBeNull();
                EndToEnd.ClientPrefix.ClientPrefixConsumer.LastReceivedMessage.Data.ShouldBe("ClientPrefixData");

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

        [Fact]
        public async Task ConventionConsumer_HandlingMultipleMessages_ShouldReceiveAllMessageTypes()
        {
            var rabbitMqContainer = CreateContainer();
            await rabbitMqContainer.StartAsync();

            try
            {
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastCreatedMessage = null;
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastUpdatedMessage = null;
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.CreatedReceived = new TaskCompletionSource<bool>();
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.UpdatedReceived = new TaskCompletionSource<bool>();

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
                    builder.AddAssemblies(typeof(EndToEnd.MultiMessage.MultiMessageConventionConsumer).Assembly)
                        .AddNamespaces("Carotte.Tests.EndToEnd.MultiMessage");
                });

                var serviceProvider = services.BuildServiceProvider();

                foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                await Task.Delay(2000);

                var createdPublisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.MultiMessage.ItemCreatedMessage>>();
                var updatedPublisher = serviceProvider.GetRequiredService<IPublisher<EndToEnd.MultiMessage.ItemUpdatedMessage>>();

                var itemId = Guid.NewGuid();
                var createdMsg = new EndToEnd.MultiMessage.ItemCreatedMessage(itemId, "Original Name");
                var updatedMsg = new EndToEnd.MultiMessage.ItemUpdatedMessage(itemId, "New Name");

                await createdPublisher.PublishAsync(createdMsg);
                await updatedPublisher.PublishAsync(updatedMsg);

                var createdReceived = await EndToEnd.MultiMessage.MultiMessageConventionConsumer.CreatedReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var updatedReceived = await EndToEnd.MultiMessage.MultiMessageConventionConsumer.UpdatedReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                createdReceived.ShouldBeTrue();
                updatedReceived.ShouldBeTrue();
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastCreatedMessage.ShouldNotBeNull();
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastCreatedMessage.Name.ShouldBe("Original Name");
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastUpdatedMessage.ShouldNotBeNull();
                EndToEnd.MultiMessage.MultiMessageConventionConsumer.LastUpdatedMessage.NewName.ShouldBe("New Name");

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
}
