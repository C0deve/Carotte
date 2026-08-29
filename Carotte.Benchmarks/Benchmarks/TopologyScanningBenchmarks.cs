using System.Collections.ObjectModel;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Carotte.Benchmarks.Config;
using Carotte.Benchmarks.Messages;

namespace Carotte.Benchmarks.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class TopologyScanningBenchmarks
{
    private HashSet<Assembly> _assemblies = null!;
    private List<string> _namespaces = null!;

    private Dictionary<string, RabbitMqOptions> _brokers = null!;
    private ReadOnlyCollection<ConsumerScanResult> _consumerScanResults = null!;
    private ReadOnlyCollection<PublisherScanResult> _publisherScanResults = null!;
    private Dictionary<string, ConsumerSettingsOptions> _consumerSettingsOverrides = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(SimpleBenchmarkMessage).Assembly, typeof(CarotteScanner).Assembly];
        _namespaces = ["Carotte.Benchmarks.Messages"];

        _brokers = new Dictionary<string, RabbitMqOptions>
        {
            ["main-broker"] = new()
            {
                Host = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                DefaultPrefetchCount = 10
            }
        };

        var (consumers, producers) = _assemblies.Scan();
        _consumerScanResults = consumers;
        _publisherScanResults = producers;

        _consumerSettingsOverrides = new Dictionary<string, ConsumerSettingsOptions>
        {
            [nameof(SimpleMessageConsumer)] = new()
            {
                PrefetchCount = 50,
                QueueName = "custom-override-queue",
                RoutingKey = "custom-routing-key",
                QueueType = "quorum"
            }
        };
    }

    [Benchmark]
    public int Scanner_FullScan()
    {
        var (consumers, producers) = _assemblies.Scan();
        return consumers.Count + producers.Count;
    }

    [Benchmark]
    public int Scanner_ScanWithNamespaceFilter()
    {
        var (consumers, producers) = _assemblies.Scan(_namespaces);
        return consumers.Count + producers.Count;
    }

    [Benchmark]
    public MessageBrokerSettings Topology_CreateSettings_Default() =>
        TopologyProvider.CreateSettings(
            _brokers,
            _consumerScanResults,
            _publisherScanResults,
            clientName: "benchmark-app");

    [Benchmark]
    public MessageBrokerSettings Topology_CreateSettings_WithOverrides() =>
        TopologyProvider.CreateSettings(
            _brokers,
            _consumerScanResults,
            _publisherScanResults,
            clientName: "benchmark-app",
            consumerSettings: _consumerSettingsOverrides);
}
