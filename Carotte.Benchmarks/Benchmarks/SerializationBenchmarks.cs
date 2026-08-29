using BenchmarkDotNet.Attributes;
using Carotte.Benchmarks.Config;
using Carotte.Benchmarks.Messages;

namespace Carotte.Benchmarks.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class SerializationBenchmarks
{
    private readonly ISerializer _serializer = new JsonSerializerImpl();

    private SimpleBenchmarkMessage _smallMessage = null!;
    private byte[] _smallData = [];

    private OrderCreatedBenchmarkMessage _mediumMessage = null!;
    private byte[] _mediumData = [];

    private LargePayloadBenchmarkMessage _largeMessage = null!;
    private byte[] _largeData = [];

    [GlobalSetup]
    public void Setup()
    {
        _smallMessage = new SimpleBenchmarkMessage("Hello Carotte Benchmark");
        _smallData = _serializer.Serialize(_smallMessage);

        var items = Enumerable.Range(1, 10)
            .Select(i => new OrderItem($"SKU-{i:D4}", i, (decimal)(i * 9.99)))
            .ToList();
        _mediumMessage = new OrderCreatedBenchmarkMessage(
            Guid.NewGuid(),
            "John Doe",
            items,
            items.Sum(it => it.Price * it.Quantity));
        _mediumData = _serializer.Serialize(_mediumMessage);

        var rawBytes = new byte[64 * 1024]; // 64 KB
        Random.Shared.NextBytes(rawBytes);
        var metadata = Enumerable.Range(1, 20)
            .ToDictionary(i => $"meta-key-{i}", i => $"meta-value-{i}-{Guid.NewGuid()}");
        _largeMessage = new LargePayloadBenchmarkMessage(
            Guid.NewGuid(),
            "Large Payload Benchmark Dataset",
            rawBytes,
            metadata);
        _largeData = _serializer.Serialize(_largeMessage);
    }

    // --- Small Payload Benchmarks ---

    [Benchmark]
    public byte[] Serialize_SmallPayload() =>
        _serializer.Serialize(_smallMessage);

    [Benchmark]
    public SimpleBenchmarkMessage? Deserialize_SmallPayload() =>
        _serializer.Deserialize<SimpleBenchmarkMessage>(_smallData);

    // --- Medium Payload Benchmarks ---

    [Benchmark]
    public byte[] Serialize_MediumPayload() =>
        _serializer.Serialize(_mediumMessage);

    [Benchmark]
    public OrderCreatedBenchmarkMessage? Deserialize_MediumPayload() =>
        _serializer.Deserialize<OrderCreatedBenchmarkMessage>(_mediumData);

    // --- Large Payload Benchmarks ---

    [Benchmark]
    public byte[] Serialize_LargePayload() =>
        _serializer.Serialize(_largeMessage);

    [Benchmark]
    public LargePayloadBenchmarkMessage? Deserialize_LargePayload() =>
        _serializer.Deserialize<LargePayloadBenchmarkMessage>(_largeData);
}
