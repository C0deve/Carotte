using System.Reflection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte;

public static class RabbitMqConsumerHost
{
    public static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
}

public sealed class RabbitMqConsumerHost<TConsumer>(
    IServiceProvider serviceProvider,
    IConnectionManager connectionManager,
    ISerializer serializer,
    ITopologyManager topologyManager,
    string broker,
    IEnumerable<QueueAttribute> queueAttributes)
    : BackgroundService
    where TConsumer : class
{
    private readonly Dictionary<Type, MethodInfo> _handlerMethods = new();
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private bool _isConnected;
    private ConsumerDelegate? _pipeline;
    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeHandlers();
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
                // Log exception if possible, or just wait before retry
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private void InitializeHandlers()
    {
        var consumerInterfaces = typeof(TConsumer).GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .ToList();

        foreach (var i in consumerInterfaces)
        {
            var messageType = i.GetGenericArguments()[0];
            var method = i.GetMethod(nameof(IConsumer<object>.HandleAsync));
            if (method != null)
            {
                _handlerMethods[messageType] = method;
            }
        }
    }

    private void BuildPipeline()
    {
        var middlewares = new List<IConsumerMiddleware>
        {
            new MetricsMiddleware(),
            new TracingMiddleware(),
            new DeserializationMiddleware(serializer),
            new ConsumerInvocationMiddleware<TConsumer>(serviceProvider, _handlerMethods)
        };

        ConsumerDelegate next = _ => Task.CompletedTask;

        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var currentNext = next;
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        _pipeline = next;
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
        var targetMessageType = ResolveMessageType(ea);
        if (targetMessageType == null) return;

        var context = new ConsumerContext(channel, ea, stoppingToken)
        {
            MessageType = targetMessageType
        };

        try
        {
            if (_pipeline != null)
            {
                await _pipeline(context);
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

    private Type? ResolveMessageType(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Type != null && _handlerMethods.Keys.Any(k => k.Name == ea.BasicProperties.Type))
        {
            return _handlerMethods.Keys.FirstOrDefault(k => k.Name == ea.BasicProperties.Type);
        }

        return _handlerMethods.Count == 1 
            ? _handlerMethods.Keys.First() 
            : _handlerMethods.Keys.FirstOrDefault();
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

    public override void Dispose()
    {
        _channel?.Dispose();
        _channelLock.Dispose();
        base.Dispose();
    }
}
