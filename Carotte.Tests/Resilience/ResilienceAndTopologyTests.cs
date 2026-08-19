using System.Text;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Carotte.Tests.Resilience;

public class ResilienceMessage;

public class ResilienceConsumer : IConsumer<ResilienceMessage>
{
    public Task HandleAsync(ResilienceMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ResilienceAndTopologyTests
{
    [Fact]
    public void ConsumerErrorStrategy_ShouldCalculateExponentialBackoffCorrectly()
    {
        var strategy = new ConsumerErrorStrategy(
            MaxRetryAttempts: 5,
            InitialRetryInterval: TimeSpan.FromMilliseconds(100),
            RetryBackoffMultiplier: 2.0,
            RetryMaxInterval: TimeSpan.FromSeconds(1),
            UseJitter: false);

        // Attempt 1 -> Initial interval (100ms)
        strategy.GetRetryDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100));

        // Attempt 2 -> 100 * 2^1 = 200ms
        strategy.GetRetryDelay(2).ShouldBe(TimeSpan.FromMilliseconds(200));

        // Attempt 3 -> 100 * 2^2 = 400ms
        strategy.GetRetryDelay(3).ShouldBe(TimeSpan.FromMilliseconds(400));

        // Attempt 4 -> 100 * 2^3 = 800ms
        strategy.GetRetryDelay(4).ShouldBe(TimeSpan.FromMilliseconds(800));

        // Attempt 5 -> 100 * 2^4 = 1600ms capped to 1000ms
        strategy.GetRetryDelay(5).ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateQueueArguments_ShouldIncludeQuorumAndCustomArguments()
    {
        var errorStrategy = new ConsumerErrorStrategy(
            DeadLetterExchange: "dlx.exchange",
            DeadLetterRoutingKey: "dlq.key");

        var customArgs = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-message-ttl"] = 60000,
            ["x-max-length"] = 1000
        };

        var arguments = ConsumerTopologyBuilder.CreateQueueArguments(errorStrategy, customArgs);

        arguments.ShouldNotBeNull();
        arguments["x-queue-type"].ShouldBe("quorum");
        arguments["x-message-ttl"].ShouldBe(60000);
        arguments["x-max-length"].ShouldBe(1000);
        arguments["x-dead-letter-exchange"].ShouldBe("dlx.exchange");
        arguments["x-dead-letter-routing-key"].ShouldBe("dlq.key");
    }

    [Fact]
    public void TopologyProvider_ShouldMapQuorumAndRetrySettingsFromConfiguration()
    {
        const string json = """
        {
          "Carotte": {
            "Brokers": {
              "default": {
                "Host": "localhost"
              }
            },
            "Consumers": {
              "ResilienceConsumer": {
                "QueueType": "quorum",
                "InitialRetryInterval": "00:00:01",
                "RetryBackoffMultiplier": 2.5,
                "Arguments": {
                  "x-delivery-limit": 5,
                  "x-max-length": 50000
                }
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
        builder.AddAssemblies(typeof(ResilienceAndTopologyTests).Assembly)
            .AddNamespaces("Carotte.Tests.Resilience");

        var (consumers, _) = builder.Assemblies.Scan(builder.Namespaces);
        var settings = TopologyProvider.CreateSettings(
            builder.Brokers,
            consumers,
            [],
            builder.ClientName,
            builder.ConsumerSettings);

        var consumer = settings.Consumers.Single(c => c.ConsumerType == typeof(ResilienceConsumer));
        consumer.Topology.Arguments.ShouldContainKey("x-queue-type");
        consumer.Topology.Arguments["x-queue-type"].ShouldBe("quorum");
        consumer.Topology.Arguments["x-delivery-limit"].ShouldBe("5");
        consumer.Topology.Arguments["x-max-length"].ShouldBe("50000");

        consumer.Topology.ErrorStrategy.InitialRetryInterval.ShouldBe(TimeSpan.FromSeconds(1));
        consumer.Topology.ErrorStrategy.RetryBackoffMultiplier.ShouldBe(2.5);
    }
}