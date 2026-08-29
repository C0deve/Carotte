using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd.MultiMessage;

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

public class MultiMessageEndToEndTests : EndToEndTestBase
{
    [Fact]
    public async Task ConventionConsumer_HandlingMultipleMessages_ShouldReceiveAllMessageTypes()
    {
        var rabbitMqContainer = CreateContainer();
        await rabbitMqContainer.StartAsync();

        try
        {
            MultiMessageConventionConsumer.LastCreatedMessage = null;
            MultiMessageConventionConsumer.LastUpdatedMessage = null;
            MultiMessageConventionConsumer.CreatedReceived = new TaskCompletionSource<bool>();
            MultiMessageConventionConsumer.UpdatedReceived = new TaskCompletionSource<bool>();

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
                builder.AddAssemblies(typeof(MultiMessageConventionConsumer).Assembly)
                    .AddNamespaces("Carotte.Tests.EndToEnd.MultiMessage");
            });

            var serviceProvider = services.BuildServiceProvider();

            foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await Task.Delay(2000);

            var createdPublisher = serviceProvider.GetRequiredService<IPublisher<ItemCreatedMessage>>();
            var updatedPublisher = serviceProvider.GetRequiredService<IPublisher<ItemUpdatedMessage>>();

            var itemId = Guid.NewGuid();
            var createdMsg = new ItemCreatedMessage(itemId, "Original Name");
            var updatedMsg = new ItemUpdatedMessage(itemId, "New Name");

            await createdPublisher.PublishAsync(createdMsg);
            await updatedPublisher.PublishAsync(updatedMsg);

            var createdReceived = await MultiMessageConventionConsumer.CreatedReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var updatedReceived = await MultiMessageConventionConsumer.UpdatedReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            createdReceived.ShouldBeTrue();
            updatedReceived.ShouldBeTrue();
            MultiMessageConventionConsumer.LastCreatedMessage.ShouldNotBeNull();
            MultiMessageConventionConsumer.LastCreatedMessage.Name.ShouldBe("Original Name");
            MultiMessageConventionConsumer.LastUpdatedMessage.ShouldNotBeNull();
            MultiMessageConventionConsumer.LastUpdatedMessage.NewName.ShouldBe("New Name");

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
