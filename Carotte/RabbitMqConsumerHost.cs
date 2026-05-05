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
    IEnumerable<QueueAttribute> queueAttributes)
    : BackgroundService
    where TConsumer : class
{
    private ConsumerPipeline? _pipeline;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogStartingRabbitmqConsumerHost(typeof(TConsumer).Name, broker);
        mediator.Initialize<TConsumer>();
        BuildPipeline();

        await rabbitMqClient.ConnectAsync(broker, cancellationToken);
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
        foreach (var attr in queueAttributes.Select(a => a.Name).Distinct())
        {
            await rabbitMqClient.BasicConsumeAsync(
                queue: attr,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                cancellationToken: stoppingToken);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SetupTopologyAsync(CancellationToken stoppingToken)
    {
        logger.LogSettingUpTopology(typeof(TConsumer).Name);
        var declaredQueues = new HashSet<string>();
        var declaredExchanges = new HashSet<string>();

        await SetupConventionTopologyAsync(declaredExchanges, stoppingToken);
        await SetupAttributeTopologyAsync(declaredQueues, declaredExchanges, stoppingToken);
    }

    private async Task SetupConventionTopologyAsync(HashSet<string> declaredExchanges, CancellationToken stoppingToken)
    {
        var consumerExchange = $"{typeof(TConsumer).Name}";
        if (declaredExchanges.Add(consumerExchange))
        {
            await rabbitMqClient.ExchangeDeclareAsync(
                exchange: consumerExchange,
                type: "fanout",
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);
        }

        foreach (var messageType in mediator.GetHandledMessageTypes())
        {
            var messageExchange = messageType.FullName ?? messageType.Name;
            if (declaredExchanges.Add(messageExchange))
            {
                await rabbitMqClient.ExchangeDeclareAsync(
                    exchange: messageExchange,
                    type: "fanout",
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);
            }

            await rabbitMqClient.ExchangeBindAsync(
                destination: consumerExchange,
                source: messageExchange,
                routingKey: "",
                cancellationToken: stoppingToken);
        }
    }

    private async Task SetupAttributeTopologyAsync(HashSet<string> declaredQueues, HashSet<string> declaredExchanges, CancellationToken stoppingToken)
    {
        var consumerExchange = $"{typeof(TConsumer).Name}";

        foreach (var attr in queueAttributes)
        {
            if (declaredQueues.Add(attr.Name))
            {
                await rabbitMqClient.QueueDeclareAsync(
                    queue: attr.Name,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    passive: false,
                    noWait: false,
                    cancellationToken: stoppingToken);

                // Bind consumer exchange to the queue
                await rabbitMqClient.QueueBindAsync(
                    queue: attr.Name,
                    exchange: consumerExchange,
                    routingKey: "",
                    cancellationToken: stoppingToken);
            }

            if (string.IsNullOrEmpty(attr.Exchange)) continue;

            if (declaredExchanges.Add(attr.Exchange))
            {
                await rabbitMqClient.ExchangeDeclareAsync(
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

            await rabbitMqClient.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        }
        catch (Exception)
        {
            await rabbitMqClient.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken: stoppingToken);
        }
    }
}
