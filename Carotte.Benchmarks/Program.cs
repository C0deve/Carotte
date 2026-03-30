using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Carotte;

namespace Carotte.Benchmarks;

public record BenchmarkMessage(string Content);

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly ISerializer _serializer = new JsonSerializerImpl();
    private readonly BenchmarkMessage _message = new("Hello Benchmark " + new string('x', 100));
    private byte[] _serializedData = [];

    [GlobalSetup]
    public void Setup()
    {
        _serializedData = _serializer.Serialize(_message);
    }

    [Benchmark]
    public byte[] Serialize() => _serializer.Serialize(_message);

    [Benchmark]
    public BenchmarkMessage? Deserialize() => _serializer.Deserialize<BenchmarkMessage>(_serializedData);
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SerializationBenchmarks>();
    }
}
