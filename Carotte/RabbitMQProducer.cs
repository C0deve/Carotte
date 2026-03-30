using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Carotte;

public class RabbitMqProducer<TMessage> : Producer, IProducer<TMessage>
{
    private readonly IConnectionManager _connectionManager;
    private readonly ISerializer _serializer;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public RabbitMqProducer(IConnectionManager connectionManager, ISerializer serializer, string broker, string exchange)
    {
        _connectionManager = connectionManager;
        _serializer = serializer;
        Broker = broker;
        Exchange = exchange;
    }

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        var routingKey = typeof(TMessage).Name;
        using var activity = CarotteDiagnostics.ActivitySource.StartActivity($"Produce {routingKey}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", Exchange);
        activity?.SetTag("messaging.destination_kind", "exchange");
        activity?.SetTag("messaging.rabbitmq.routing_key", routingKey);

        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_channel == null || !_channel.IsOpen)
                {
                    var connection = await _connectionManager.GetConnectionAsync(Broker);
                    _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                }

                var body = _serializer.Serialize(message);
                
                var properties = new BasicProperties
                {
                    Type = routingKey
                };
                
                if (activity != null)
                {
                    var context = new PropagationContext(activity.Context, Baggage.Current);
                    Propagator.Inject(context, properties, (props, key, value) =>
                    {
                        props.Headers ??= new Dictionary<string, object?>();
                        props.Headers[key] = value;
                    });
                }
                
                await _channel.BasicPublishAsync(
                    exchange: Exchange,
                    routingKey: routingKey,
                    body: body,
                    basicProperties: properties,
                    mandatory: true,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _lock.Release();
            }

            CarotteDiagnostics.MessagesProducedCounter.Add(1, new KeyValuePair<string, object?>("exchange", Exchange));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
