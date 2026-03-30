using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carotte;

public class RabbitMQConsumerHost<TConsumer>(
    IServiceProvider serviceProvider,
    IConnectionManager connectionManager,
    ISerializer serializer,
    ITopologyManager topologyManager,
    string broker,
    IEnumerable<QueueAttribute> queueAttributes)
    : BackgroundService
    where TConsumer : class
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly Dictionary<Type, MethodInfo> _handlerMethods = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On récupère toutes les interfaces IConsumer<T> implémentées par TConsumer
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

        var connection = await connectionManager.GetConnectionAsync(broker);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Déclaration de la topologie (si exchange spécifié dans l'attribut)
        foreach (var attr in queueAttributes)
        {
            await channel.QueueDeclareAsync(
                queue: attr.Name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            if (!string.IsNullOrEmpty(attr.Exchange))
            {
                await channel.ExchangeDeclareAsync(
                    exchange: attr.Exchange,
                    type: "topic", // Par défaut topic pour la flexibilité
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

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var parentContext = Propagator.Extract(default, ea.BasicProperties, (props, key) =>
            {
                if (props.Headers != null && props.Headers.TryGetValue(key, out var value))
                {
                    if (value is byte[] bytes) return [Encoding.UTF8.GetString(bytes)];
                    return [value?.ToString() ?? string.Empty];
                }
                return [];
            });

            // On essaie de déterminer le type de message. 
            // Pour l'instant, on suppose que le type est stocké dans les headers ou on essaie de matcher avec les interfaces.
            // Une approche simple est d'essayer de désérialiser dans chaque type géré, mais c'est inefficace.
            // On va utiliser le nom du type complet comme discriminant par défaut dans les headers si présent, 
            // sinon on prend le premier type géré (comportement par défaut si un seul type).
            
            Type? targetMessageType = null;
            if (ea.BasicProperties.Type != null && _handlerMethods.Keys.Any(k => k.Name == ea.BasicProperties.Type))
            {
                 targetMessageType = _handlerMethods.Keys.FirstOrDefault(k => k.Name == ea.BasicProperties.Type);
            }
            
            if (targetMessageType == null && _handlerMethods.Count == 1)
            {
                targetMessageType = _handlerMethods.Keys.First();
            }

            if (targetMessageType == null)
            {
                // On peut aussi essayer de regarder le routing key ou d'autres headers
                // Pour cet exercice, on va prendre le premier si non spécifié
                targetMessageType = _handlerMethods.Keys.FirstOrDefault();
            }

            if (targetMessageType == null) return;

            using var activity = CarotteDiagnostics.ActivitySource.StartActivity(
                $"Consume {targetMessageType.Name}", 
                ActivityKind.Consumer,
                parentContext.ActivityContext);

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", ea.RoutingKey);
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var body = ea.Body.ToArray();
                var deserializeMethod = serializer.GetType().GetMethod(nameof(ISerializer.Deserialize))?.MakeGenericMethod(targetMessageType);
                var message = deserializeMethod?.Invoke(serializer, [body]);

                if (message != null)
                {
                    var handler = serviceProvider.GetRequiredService<TConsumer>();
                    var task = (Task?)_handlerMethods[targetMessageType].Invoke(handler, [message, stoppingToken]);
                    if (task != null) await task;
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
                CarotteDiagnostics.MessagesConsumedCounter.Add(1, new KeyValuePair<string, object?>("queue", ea.RoutingKey));
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                CarotteDiagnostics.MessageErrorsCounter.Add(1, new KeyValuePair<string, object?>("queue", ea.RoutingKey));
                
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
            finally
            {
                stopwatch.Stop();
                CarotteDiagnostics.MessageProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("queue", ea.RoutingKey));
            }
        };

        foreach (var attr in queueAttributes)
        {
            await channel.BasicConsumeAsync(queue: attr.Name, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await _channelLock.WaitAsync(CancellationToken.None);
            try
            {
                if (channel.IsOpen)
                {
                    await channel.CloseAsync();
                }
            }
            finally
            {
                _channelLock.Release();
            }
            channel.Dispose();
            _channelLock.Dispose();
        }
    }
}
