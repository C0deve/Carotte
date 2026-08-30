using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using Carotte.Pipeline;

namespace Carotte;

/// <summary>
/// Hosted background service managing the lifecycle and message consumption for a specific consumer <typeparamref name="TConsumer"/>.
/// Handles connection setup, topology provisioning, QoS prefetch configuration, pipeline execution,
/// scoped service resolution, retry policies, and ACK/NACK error strategies (dead-lettering or requeueing).
/// </summary>
/// <typeparam name="TConsumer">The consumer class implementing one or more <see cref="IConsumer{TMessage}"/> interfaces.</typeparam>
internal sealed class RabbitMqConsumerHost<TConsumer>(
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

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        mediator.Initialize<TConsumer>();

        var exchanges = topology switch
        {
            ConsumerConventionTopology conv => string.Join(", ", conv.MessageExchangeNames.Union([conv.ConsumerExchangeName])),
            ConsumerAttributeTopology attr => string.Join(", ", attr.Bindings.Select(b => b.ExchangeSource).Distinct()),
            _ => "Unknown"
        };
        var messageTypes = string.Join(", ", mediator.GetHandledMessageTypes().Select(t => $"'{t.Name}'"));

        logger.LogStartingRabbitmqConsumerHost(typeof(TConsumer).Name, broker, topology.Queue, exchanges, messageTypes);
        BuildPipeline();

        await rabbitMqClient.ConnectAsync(topology.Broker, cancellationToken);

        // Configure prefetch count (QoS)
        await rabbitMqClient.BasicQosAsync(0, topology.PrefetchCount, false, cancellationToken);

        // Hook incoming message handler
        rabbitMqClient.ReceivedAsync += (_, ea) => HandleMessageAsync(ea, CancellationToken.None);

        // Declare topology (exchanges, queues, DLX, bindings)
        await SetupTopologyAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogStoppingRabbitmqConsumerHost(typeof(TConsumer).Name);

        try
        {
            await rabbitMqClient.CloseAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while closing RabbitMqClient in consumer host {ConsumerType}", typeof(TConsumer).Name);
        }

        try
        {
            await rabbitMqClient.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while disposing RabbitMqClient in consumer host {ConsumerType}", typeof(TConsumer).Name);
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Background execution loop that maintains active consumption from the RabbitMQ queue.
    /// In case of transient connection losses or unexpected exceptions, retries after a 5-second delay.
    /// </summary>
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
                // Graceful cancellation on host shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while executing consumer {ConsumerType}. Retrying in 5 seconds...", typeof(TConsumer).Name);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Builds the middleware execution pipeline for incoming messages.
    /// Middlewares execute in the following order:
    /// Metrics -> Tracing (OTel) -> Deserialization (JSON) -> Consumer Invocation (Mediator).
    /// </summary>
    private void BuildPipeline()
    {
        _pipeline = new ConsumerPipelineBuilder()
            .Use(new MetricsMiddleware())
            .Use(new TracingMiddleware())
            .Use(new DeserializationMiddleware(serializer))
            .Use(new ConsumerInvocationMiddleware(mediator))
            .Build();
    }

    /// <summary>
    /// Starts basic consuming on the queue and pauses indefinitely until cancellation is requested.
    /// </summary>
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

    /// <summary>
    /// Declares the full RabbitMQ topology for this consumer.
    /// </summary>
    private async Task SetupTopologyAsync(CancellationToken stoppingToken)
    {
        logger.LogSettingUpTopology(typeof(TConsumer).Name);
        await ConsumerTopologyBuilder.BuildAsync(rabbitMqClient, topology, stoppingToken);
    }

    /// <summary>
    /// Handles a delivered message from RabbitMQ:
    /// 1. Resolves target message type from metadata/properties.
    /// 2. Creates an isolated async service scope for resolving dependencies.
    /// 3. Executes the consumer pipeline with retry.
    /// 4. ACKs the message on success or NACKs on unrecoverable failure (routing to DLQ or requeuing).
    /// </summary>
    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);
        var targetMessageType = mediator.ResolveMessageType(ea);
        if (targetMessageType == null)
        {
            var supportedTypes = string.Join(", ", mediator.GetHandledMessageTypes().Select(t => t.Name));
            logger.LogWarning(
                "Unable to resolve message type for consumer {ConsumerType}. RabbitMQ Type property: {MessageType}. Exchange: {Exchange}. RoutingKey: {RoutingKey}. Queue: {Queue}. DeliveryTag: {DeliveryTag}. Supported types: [{SupportedTypes}]. Nacking without requeue.",
                typeof(TConsumer).Name,
                ea.BasicProperties.Type,
                ea.Exchange,
                ea.RoutingKey,
                topology.Queue,
                ea.DeliveryTag,
                supportedTypes);

            await rabbitMqClient.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken: stoppingToken);
            return;
        }

        await using var messageScope = mediator.CreateMessageScope();
        var context = new ConsumerContext(ea, messageScope.ServiceProvider, CancellationToken: stoppingToken)
        {
            MessageType = targetMessageType
        };

        try
        {
            await ExecutePipelineWithRetryAsync(context);
            await rabbitMqClient.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Message processing failed for consumer {ConsumerType}. DeliveryTag: {DeliveryTag}. Nacking with requeue={Requeue}.",
                typeof(TConsumer).Name,
                ea.DeliveryTag,
                errorStrategy.RequeueOnFailure);

            await rabbitMqClient.BasicNackAsync(
                ea.DeliveryTag,
                false,
                errorStrategy.RequeueOnFailure,
                cancellationToken: stoppingToken);
        }
    }

    /// <summary>
    /// Executes the consumer pipeline, applying retry intervals and exponential backoff
    /// based on the configured error strategy when transient errors occur.
    /// </summary>
    private async Task ExecutePipelineWithRetryAsync(ConsumerContext context)
    {
        var errorStrategy = topology.ErrorStrategy.WithConventionDefaults(topology.Queue);
        var maxRetryAttempts = Math.Max(0, errorStrategy.EffectiveMaxRetryAttempts);
        var attempt = 0;

        while (true)
        {
            try
            {
                if (_pipeline != null)
                {
                    await _pipeline.ExecuteAsync(context);
                }

                return;
            }
            catch (Exception ex) when (attempt < maxRetryAttempts && IsRetryable(ex))
            {
                attempt++;
                var delay = errorStrategy.GetRetryDelay(attempt);

                logger.LogWarning(
                    ex,
                    "Message processing failed for consumer {ConsumerType}. Retrying attempt {RetryAttempt}/{MaxRetryAttempts} after delay {RetryDelay}.",
                    typeof(TConsumer).Name,
                    attempt,
                    maxRetryAttempts,
                    delay);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, context.CancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Determines whether an exception is transient and eligible for in-memory retry.
    /// Non-transient errors (like JSON deserialization failure) skip in-memory retry and proceed to DLQ/NACK immediately.
    /// </summary>
    private static bool IsRetryable(Exception ex)
    {
        if (ex is System.Text.Json.JsonException)
        {
            return false;
        }

        return true;
    }
}
