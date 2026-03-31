using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;
using Carotte.pipeline;

namespace Carotte.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task ConsumerPipelineBuilder_ShouldBuildCorrectPipeline()
    {
        // Arrange
        string[] expected = ["m1-start", "m2-start", "target", "m2-end", "m1-end"];
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

        List<string> executionOrder = [];

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

        var targetMiddleware = new Mock<IConsumerMiddleware>();
        targetMiddleware.Setup(m => m.InvokeAsync(It.IsAny<ConsumerContext>(), It.IsAny<ConsumerDelegate>()))
            .Returns(async (ConsumerContext ctx, ConsumerDelegate next) =>
            {
                executionOrder.Add("target");
                await next(ctx);
            });

        // Act
        var pipeline = new ConsumerPipelineBuilder()
            .Use(middleware1.Object)
            .Use(middleware2.Object)
            .Use(targetMiddleware.Object)
            .Build();

        await pipeline.ExecuteAsync(context);

        // Assert
        executionOrder.ShouldBe(expected);
    }
}
