using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Events;
using Carotte.pipeline;

namespace Carotte;

public sealed class RabbitMqConsumerHost<TConsumer>(
    ConsumerMediator mediator,
    IRabbitMqClient rabbitMqClient,
    ISerializer serializer,
    string broker,
    IEnumerable<QueueAttribute> queueAttributes)
    : BackgroundService
    where TConsumer : class
{
    private bool _isConnected;
    private ConsumerPipeline? _pipeline;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        mediator.Initialize<TConsumer>();
        BuildPipeline();

        await SetupTopologyAsync(cancellationToken);
        _isConnected = true;

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_isConnected)
                {
                    await SetupTopologyAsync(stoppingToken);
                    _isConnected = true;
                }

                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal exit
            }
            catch (Exception)
            {
                _isConnected = false;
                // Log exception if possible, or just wait before retrying
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
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
        var channel = await rabbitMqClient.GetChannelAsync(broker, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => 
            HandleMessageAsync(ea, stoppingToken);

        foreach (var attr in queueAttributes.Select(a => a.Name).Distinct())
        {
            await rabbitMqClient.BasicConsumeAsync(
                broker: broker,
                queue: attr,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: stoppingToken);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SetupTopologyAsync(CancellationToken stoppingToken)
    {
        var declaredQueues = new HashSet<string>();
        var declaredExchanges = new HashSet<string>();

        foreach (var attr in queueAttributes)
        {
            if (declaredQueues.Add(attr.Name))
            {
                await rabbitMqClient.QueueDeclareAsync(
                    broker: broker,
                    queue: attr.Name,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    passive: false,
                    noWait: false,
                    cancellationToken: stoppingToken);
            }

            if (string.IsNullOrEmpty(attr.Exchange)) continue;

            if (declaredExchanges.Add(attr.Exchange))
            {
                await rabbitMqClient.ExchangeDeclareAsync(
                    broker: broker,
                    exchange: attr.Exchange,
                    type: "topic",
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    passive: false,
                    noWait: false,
                    cancellationToken: stoppingToken);
            }

            await rabbitMqClient.QueueBindAsync(
                broker: broker,
                queue: attr.Name,
                exchange: attr.Exchange,
                routingKey: attr.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);
        }
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

            await rabbitMqClient.BasicAckAsync(broker, ea.DeliveryTag, false, cancellationToken: stoppingToken);
        }
        catch (Exception)
        {
            await rabbitMqClient.BasicNackAsync(broker, ea.DeliveryTag, false, true, cancellationToken: stoppingToken);
        }
    }
}
