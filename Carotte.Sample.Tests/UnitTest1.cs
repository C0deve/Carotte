using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Carotte.Sample.Tests;

public class Tests
{
    [Fact]
    public async Task Test1()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddCarotte(c => c
                .AddBroker("my-broker", _ => { })
                .AddAssemblies(typeof(OrderCreated).Assembly))
            .AddCarotteTestKit()
            .BuildServiceProvider();
        
        var testKit = sp.GetRequiredService<CarotteTestKit>();
        
        var testMessage = new OrderCreated(
            Guid.NewGuid(),
            "John Doe",
            100);

        // Act
        await testKit.SimulateReceiveAsync<OrderConsumer, OrderCreated>(testMessage);

        // Assert
        var sentMessages = testKit.GetSentMessages<NotificationMessage>();
        sentMessages.Count.ShouldBe(1);
        sentMessages[0].OrderId.ShouldBe(testMessage.OrderId);
    }
}
