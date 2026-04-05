using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Carotte.pipeline;

namespace Carotte;

public sealed class RabbitMqConsumerHost<TConsumer>(
    ConsumerMediator mediator,
    IConnectionManager connectionManager,
    ISerializer serializer,
    string broker,
    IEnumerable<QueueAttribute> queueAttributes)
    : BackgroundService
    where TConsumer : class
{
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private bool _isConnected;
    private ConsumerPipeline? _pipeline;
    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        mediator.Initialize<TConsumer>();
        BuildPipeline();

        _connection = await connectionManager.GetConnectionAsync(broker);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _isConnected = true;

        await SetupTopologyAsync(_channel, cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await CloseChannelAsync(_channel);
        }

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_isConnected || _channel == null || !_channel.IsOpen)
                {
                    _connection = await connectionManager.GetConnectionAsync(broker);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    _isConnected = true;
                    await SetupTopologyAsync(_channel, stoppingToken);
                }

                await ConsumeAsync(_channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal exit
            }
            catch (Exception)
            {
                _isConnected = false;
                if (_channel != null)
                {
                    await CloseChannelAsync(_channel);
                    _channel = null;
                }
                // Log exception if possible, or just wait before retrying
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _channelLock.Dispose();
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

    private async Task ConsumeAsync(IChannel channel, CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => 
            HandleMessageAsync(channel, ea, stoppingToken);

        foreach (var attr in queueAttributes)
        {
            await channel.BasicConsumeAsync(
                queue: attr.Name,
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

    private async Task SetupTopologyAsync(IChannel channel, CancellationToken stoppingToken)
    {
        foreach (var attr in queueAttributes)
        {
            await channel.QueueDeclareAsync(
                queue: attr.Name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: stoppingToken);

            if (string.IsNullOrEmpty(attr.Exchange)) continue;
            
            await channel.ExchangeDeclareAsync(
                exchange: attr.Exchange,
                type: "topic",
                durable: true,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: attr.Name,
                exchange: attr.Exchange,
                routingKey: attr.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);
        }
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
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

            await _channelLock.WaitAsync(stoppingToken);
            try
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
            }
            finally
            {
                _channelLock.Release();
            }
        }
        catch (Exception)
        {
            await _channelLock.WaitAsync(stoppingToken);
            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken: stoppingToken);
            }
            finally
            {
                _channelLock.Release();
            }
        }
    }

    private async Task CloseChannelAsync(IChannel channel)
    {
        await _channelLock.WaitAsync(CancellationToken.None);
        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None);
            }
        }
        catch
        {
            // Ignore errors during close
        }
        finally
        {
            _channelLock.Release();
        }
        channel.Dispose();
    }
}
