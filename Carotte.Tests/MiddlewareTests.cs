using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;

namespace Carotte.Tests;

public class MiddlewareTests
{
    private static readonly string[] Expected = ["m1-start", "m2-start", "target", "m2-end", "m1-end"];

    [Fact]
    public async Task Pipeline_ShouldExecuteMiddlewaresInOrder()
    {
        // Arrange
        var channel = new Mock<IChannel>();
        var ea = new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            properties: new Mock<IReadOnlyBasicProperties>().Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);
        var context = new ConsumerContext(channel.Object, ea, CancellationToken.None);
        
        var executionOrder = new List<string>();

        var middleware1 = new Mock<IConsumerMiddleware>();
        middleware1.Setup(m => m.InvokeAsync(It.IsAny<ConsumerContext>(), It.IsAny<ConsumerDelegate>()))
            .Returns(async (ConsumerContext ctx, ConsumerDelegate next) =>
            {
                executionOrder.Add("m1-start");
                await next(ctx);
                executionOrder.Add("m1-end");
            });

        var middleware2 = new Mock<IConsumerMiddleware>();
        middleware2.Setup(m => m.InvokeAsync(It.IsAny<ConsumerContext>(), It.IsAny<ConsumerDelegate>()))
            .Returns(async (ConsumerContext ctx, ConsumerDelegate next) =>
            {
                executionOrder.Add("m2-start");
                await next(ctx);
                executionOrder.Add("m2-end");
            });

        // Simuler la construction du pipeline dans RabbitMqConsumerHost
        var middlewares = new List<IConsumerMiddleware> { middleware1.Object, middleware2.Object };
        ConsumerDelegate next = _ =>
        {
            executionOrder.Add("target");
            return Task.CompletedTask;
        };

        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var currentNext = next;
            next = ctx => middleware.InvokeAsync(ctx, currentNext);
        }

        // Act
        await next(context);

        // Assert
        executionOrder.ShouldBe(Expected);
    }
}
