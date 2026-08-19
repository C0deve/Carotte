using System.Text;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Carotte.Tests.Overrides;

public class OrderEvent;

[Queue("attribute-queue", broker: "test-broker", prefetchCount: 2, maxRetryAttempts: 1)]
public class ConfigurableConsumer : IConsumer<OrderEvent>
{
    public Task HandleAsync(OrderEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ConventionConsumer : IConsumer<OrderEvent>
{
    public Task HandleAsync(OrderEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ConsumerOverridesTests
{
    [Fact]
    public void Configuration_ShouldOverrideConsumerAttributes()
    {
        var json = """
        {
          "Carotte": {
            "Brokers": {
              "test-broker": {
                "Host": "localhost"
              }
            },
            "Consumers": {
              "ConfigurableConsumer": {
                "PrefetchCount": 25,
                "MaxRetryAttempts": 8,
                "QueueName": "overridden-queue-name",
                "DeadLetterExchange": "custom.dlx",
                "DeadLetterQueue": "custom.dlq"
              }
            }
          }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        var builder = new CarotteBuilder();
        var options = new CarotteOptions();
        configuration.GetSection("Carotte").Bind(options);

        foreach (var (k, v) in options.Brokers) builder.AddBroker(k, v);
        foreach (var (k, v) in options.Consumers) builder.ConsumerSettings[k] = v;
        builder.AddAssemblies(typeof(ConsumerOverridesTests).Assembly)
               .AddNamespaces("Carotte.Tests.Overrides");

        var (consumers, _) = builder.Assemblies.Scan(builder.Namespaces);
        var settings = TopologyProvider.CreateSettings(
            builder.Brokers,
            consumers,
            [],
            builder.ClientName,
            builder.ConsumerSettings);

        var consumer = settings.Consumers.Single(c => c.ConsumerType == typeof(ConfigurableConsumer));
        consumer.Topology.PrefetchCount.ShouldBe((ushort)25);
        consumer.Topology.Queue.ShouldBe("overridden-queue-name");
        consumer.Topology.ErrorStrategy.MaxRetryAttempts.ShouldBe(8);
        consumer.Topology.ErrorStrategy.DeadLetterExchange.ShouldBe("custom.dlx");
        consumer.Topology.ErrorStrategy.DeadLetterQueue.ShouldBe("custom.dlq");
    }

    [Fact]
    public void Configuration_ShouldOverrideConventionConsumerSettings()
    {
        var json = """
        {
          "Carotte": {
            "Brokers": {
              "test-broker": {
                "Host": "localhost"
              }
            },
            "Consumers": {
              "ConventionConsumer": {
                "PrefetchCount": 50,
                "MaxRetryAttempts": 0
              }
            }
          }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        var builder = new CarotteBuilder();
        var options = new CarotteOptions();
        configuration.GetSection("Carotte").Bind(options);

        foreach (var (k, v) in options.Brokers) builder.AddBroker(k, v);
        foreach (var (k, v) in options.Consumers) builder.ConsumerSettings[k] = v;
        builder.AddAssemblies(typeof(ConsumerOverridesTests).Assembly)
               .AddNamespaces("Carotte.Tests.Overrides");

        var (consumers, _) = builder.Assemblies.Scan(builder.Namespaces);
        var settings = TopologyProvider.CreateSettings(
            builder.Brokers,
            consumers,
            [],
            builder.ClientName,
            builder.ConsumerSettings);

        var consumer = settings.Consumers.Single(c => c.ConsumerType == typeof(ConventionConsumer));
        consumer.Topology.PrefetchCount.ShouldBe((ushort)50);
        consumer.Topology.ErrorStrategy.MaxRetryAttempts.ShouldBe(0);
    }
}
