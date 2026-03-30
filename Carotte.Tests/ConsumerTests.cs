using Shouldly;

namespace Carotte.Tests;

public record TestMessage(string Content);

public class MessageConsumer : Consumer, IConsumer<TestMessage>
{
    public MessageConsumer()
    {
        Broker = "DefaultBroker";
        Queue = "TestQueue";
    }

    public Task HandleAsync(TestMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class ConsumerTests
{
    [Fact]
    public void MessageConsumer_ShouldImplementIConsumer()
    {
        var consumer = new MessageConsumer();
        consumer.ShouldBeAssignableTo<IConsumer<TestMessage>>();
    }
    
    [Fact]
    public void MessageConsumer_ShouldInheritFromConsumer()
    {
        var consumer = new MessageConsumer();
        consumer.ShouldBeAssignableTo<Consumer>();
    }

    [Fact]
    public void MessageConsumer_ShouldHaveBrokerAndQueueSet()
    {
        var consumer = new MessageConsumer();
        consumer.Broker.ShouldBe("DefaultBroker");
        consumer.Queue.ShouldBe("TestQueue");
    }
}
