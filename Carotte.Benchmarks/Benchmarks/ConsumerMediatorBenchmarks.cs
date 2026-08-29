using BenchmarkDotNet.Attributes;
using Carotte.Benchmarks.Config;
using Carotte.Benchmarks.Messages;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte.Benchmarks.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class ConsumerMediatorBenchmarks
{
    private ServiceProvider _serviceProvider = null!;
    private ConsumerMediator _singleMediator = null!;
    private ConsumerMediator _multiMediator = null!;
    private BasicDeliverEventArgs _deliveryArgs = null!;
    private SimpleBenchmarkMessage _message = null!;
    private IConsumer<SimpleBenchmarkMessage> _directConsumer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageTypeResolver, MessageTypeResolver>();
        services.AddTransient<SimpleMessageConsumer>();
        services.AddTransient<MultiMessageConsumer>();

        _serviceProvider = services.BuildServiceProvider();

        _singleMediator = new ConsumerMediator(_serviceProvider);
        _singleMediator.Initialize<SimpleMessageConsumer>();

        _multiMediator = new ConsumerMediator(_serviceProvider);
        _multiMediator.Initialize<MultiMessageConsumer>();

        _deliveryArgs = new BasicDeliverEventArgs(
            consumerTag: "benchmark-consumer",
            deliveryTag: 1,
            redelivered: false,
            exchange: "benchmark-exchange",
            routingKey: nameof(SimpleBenchmarkMessage),
            properties: new BasicProperties { Type = nameof(SimpleBenchmarkMessage) },
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);

        _message = new SimpleBenchmarkMessage("Hello Benchmark");
        _directConsumer = new SimpleMessageConsumer();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    [Benchmark]
    public Type? ResolveMessageType() =>
        _singleMediator.ResolveMessageType(_deliveryArgs);

    [Benchmark]
    public AsyncServiceScope CreateMessageScope() =>
        _singleMediator.CreateMessageScope();

    [Benchmark]
    public async Task InvokeAsync_SingleMessageConsumer()
    {
        await using var scope = _singleMediator.CreateMessageScope();
        await _singleMediator.InvokeAsync(
            scope.ServiceProvider,
            typeof(SimpleBenchmarkMessage),
            _message,
            CancellationToken.None);
    }

    [Benchmark]
    public async Task InvokeAsync_MultiMessageConsumer()
    {
        await using var scope = _multiMediator.CreateMessageScope();
        await _multiMediator.InvokeAsync(
            scope.ServiceProvider,
            typeof(SimpleBenchmarkMessage),
            _message,
            CancellationToken.None);
    }

    [Benchmark]
    public Task DirectInterfaceInvokeAsync() =>
        _directConsumer.HandleAsync(_message, CancellationToken.None);
}
