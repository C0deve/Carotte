using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using Carotte.pipeline;

namespace Carotte;

public sealed class RabbitMqConsumerHost<TConsumer>(
    ConsumerMediator mediator,
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    ILogger<RabbitMqConsumerHost<TConsumer>> logger,
    string broker,
    IConsumerTopology topology)
    : BackgroundService
    where TConsumer : class
{
    private ConsumerPipeline? _pipeline;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogStartingRabbitmqConsumerHost(typeof(TConsumer).Name, broker);
        mediator.Initialize<TConsumer>();
        BuildPipeline();

        await rabbitMqClient.ConnectAsync(topology.Broker, cancellationToken);
        rabbitMqClient.ReceivedAsync += (_, ea) => HandleMessageAsync(ea, CancellationToken.None);
        
        await SetupTopologyAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogStoppingRabbitmqConsumerHost(typeof(TConsumer).Name);
        
        await rabbitMqClient.CloseAsync(cancellationToken);
        await rabbitMqClient.DisposeAsync();
        
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal exit
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while executing consumer {ConsumerType}. Retrying in 5 seconds...", typeof(TConsumer).Name);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private void BuildPipeline()
    {
        _pipeline = new ConsumerPipelineBuilder()
            .Use(new MetricsMiddleware())
            .Use(new TracingMiddleware())
            .Use(new DeserializationMiddleware(serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await rabbitMqClient.BasicConsumeAsync(
            queue: topology.Queue,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SetupTopologyAsync(CancellationToken stoppingToken)
    {
        logger.LogSettingUpTopology(typeof(TConsumer).Name);
        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient, topology, stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var targetMessageType = mediator.ResolveMessageType(ea);
        if (targetMessageType == null) return;

        var context = new ConsumerContext(ea, stoppingToken)
        {
            MessageType = targetMessageType
        };

        try
        {
            if (_pipeline != null)
            {
                await _pipeline.ExecuteAsync(context);
            }

            await rabbitMqClient.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        }
        catch (Exception)
        {
            await rabbitMqClient.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken: stoppingToken);
        }
    }
}
