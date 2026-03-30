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

public class RabbitMqConsumerHost<TConsumer>(
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InitializeHandlers();
        BuildPipeline();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _isConnected = false;
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

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionManager.GetConnectionAsync(broker);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _isConnected = true;

        try
        {
            await SetupTopologyAsync(channel, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => 
                HandleMessageAsync(channel, ea, stoppingToken);

            foreach (var attr in queueAttributes)
            {
                await channel.BasicConsumeAsync(queue: attr.Name, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            _isConnected = false;
            await CloseChannelAsync(channel);
        }
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
                cancellationToken: stoppingToken);

            if (string.IsNullOrEmpty(attr.Exchange)) continue;
            
            await channel.ExchangeDeclareAsync(
                exchange: attr.Exchange,
                type: "topic",
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: attr.Name,
                exchange: attr.Exchange,
                routingKey: attr.RoutingKey,
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
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
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
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
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

        if (_handlerMethods.Count == 1)
        {
            return _handlerMethods.Keys.First();
        }

        return _handlerMethods.Keys.FirstOrDefault();
    }

    private async Task CloseChannelAsync(IChannel channel)
    {
        await _channelLock.WaitAsync(CancellationToken.None);
        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync();
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
        _channelLock.Dispose();
        base.Dispose();
    }
}
