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
        var ea = new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            properties: new Mock<IReadOnlyBasicProperties>().Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);
        var context = new ConsumerContext(ea, Mock.Of<IServiceProvider>(), CancellationToken: CancellationToken.None);

        List<string> executionOrder = [];

        var middleware1 = new RecordingMiddleware(executionOrder, "m1-start", "m1-end");
        var middleware2 = new RecordingMiddleware(executionOrder, "m2-start", "m2-end");
        var targetMiddleware = new RecordingMiddleware(executionOrder, "target");

        // Act
        var pipeline = new ConsumerPipelineBuilder()
            .Use(middleware1)
            .Use(middleware2)
            .Use(targetMiddleware)
            .Build();

        await pipeline.ExecuteAsync(context);

        // Assert
        executionOrder.ShouldBe(expected);
    }

    private sealed class RecordingMiddleware(List<string> executionOrder, string start, string? end = null)
        : IConsumerMiddleware
    {
        public async Task InvokeAsync(ConsumerContext context, ConsumerDelegate next)
        {
            executionOrder.Add(start);
            await next(context);
            if (end is not null) executionOrder.Add(end);
        }
    }
}
