using BenchmarkDotNet.Attributes;
using Carotte.Benchmarks.Config;
using Carotte.Benchmarks.Messages;
using Carotte.pipeline;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte.Benchmarks.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class PipelineExecutionBenchmarks
{
    private ServiceProvider _serviceProvider = null!;
    private ISerializer _serializer = null!;
    private SimpleBenchmarkMessage _message = null!;
    private byte[] _serializedPayload = [];
    private BasicDeliverEventArgs _deliveryArgs = null!;

    private PublisherPipeline<SimpleBenchmarkMessage> _publisherFullPipeline = null!;
    private PublisherPipeline<SimpleBenchmarkMessage> _publisherSerializationOnlyPipeline = null!;

    private ConsumerPipeline _consumerFullPipeline = null!;
    private ConsumerPipeline _consumerMinimalPipeline = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageTypeResolver, MessageTypeResolver>();
        services.AddSingleton<ISerializer, JsonSerializerImpl>();
        services.AddTransient<SimpleMessageConsumer>();

        _serviceProvider = services.BuildServiceProvider();
        _serializer = _serviceProvider.GetRequiredService<ISerializer>();

        _message = new SimpleBenchmarkMessage("Hello Pipeline Benchmark");
        _serializedPayload = _serializer.Serialize(_message);

        // --- Setup Publisher Pipelines ---
        _publisherFullPipeline = new PublisherPipelineBuilder<SimpleBenchmarkMessage>()
            .Use(new PublisherTracingMiddleware<SimpleBenchmarkMessage>())
            .Use(new PublisherMetricsMiddleware<SimpleBenchmarkMessage>())
            .Use(new SerializationMiddleware<SimpleBenchmarkMessage>(_serializer))
            .Build();

        _publisherSerializationOnlyPipeline = new PublisherPipelineBuilder<SimpleBenchmarkMessage>()
            .Use(new SerializationMiddleware<SimpleBenchmarkMessage>(_serializer))
            .Build();

        // --- Setup Consumer Pipelines ---
        var mediator = new ConsumerMediator(_serviceProvider);
        mediator.Initialize<SimpleMessageConsumer>();

        _consumerFullPipeline = new ConsumerPipelineBuilder()
            .Use(new TracingMiddleware())
            .Use(new MetricsMiddleware())
            .Use(new DeserializationMiddleware(_serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();

        _consumerMinimalPipeline = new ConsumerPipelineBuilder()
            .Use(new DeserializationMiddleware(_serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();

        _deliveryArgs = new BasicDeliverEventArgs(
            consumerTag: "benchmark-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: "benchmark-exchange",
            routingKey: nameof(SimpleBenchmarkMessage),
            properties: new BasicProperties { Type = nameof(SimpleBenchmarkMessage) },
            body: _serializedPayload,
            cancellationToken: CancellationToken.None);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    [Benchmark]
    public async Task PublisherPipeline_Full()
    {
        var context = new PublisherContext<SimpleBenchmarkMessage>(
            _message,
            "benchmark-exchange",
            nameof(SimpleBenchmarkMessage),
            TypeIdentifier: nameof(SimpleBenchmarkMessage));

        await _publisherFullPipeline.ExecuteAsync(context);
    }

    [Benchmark]
    public async Task PublisherPipeline_SerializationOnly()
    {
        var context = new PublisherContext<SimpleBenchmarkMessage>(
            _message,
            "benchmark-exchange",
            nameof(SimpleBenchmarkMessage),
            TypeIdentifier: nameof(SimpleBenchmarkMessage));

        await _publisherSerializationOnlyPipeline.ExecuteAsync(context);
    }

    [Benchmark]
    public async Task ConsumerPipeline_Full()
    {
        var context = new ConsumerContext(
            _deliveryArgs,
            _serviceProvider,
            Message: null,
            MessageType: typeof(SimpleBenchmarkMessage),
            CancellationToken: CancellationToken.None);

        await _consumerFullPipeline.ExecuteAsync(context);
    }

    [Benchmark]
    public async Task ConsumerPipeline_DeserializationAndInvocation()
    {
        var context = new ConsumerContext(
            _deliveryArgs,
            _serviceProvider,
            Message: null,
            MessageType: typeof(SimpleBenchmarkMessage),
            CancellationToken: CancellationToken.None);

        await _consumerMinimalPipeline.ExecuteAsync(context);
    }
}
