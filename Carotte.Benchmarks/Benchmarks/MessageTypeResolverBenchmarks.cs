using BenchmarkDotNet.Attributes;
using Carotte.Benchmarks.Config;
using Carotte.Benchmarks.Messages;

namespace Carotte.Benchmarks.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class MessageTypeResolverBenchmarks
{
    private readonly IMessageTypeResolver _resolver = MessageTypeResolver.Default;

    private Type[] _candidates = [];
    private string _simpleName = string.Empty;
    private string _fullName = string.Empty;
    private string _customAlias = string.Empty;
    private string _assemblyQualifiedName = string.Empty;
    private string _urnName = string.Empty;

    [Params(1, 5, 20)]
    public int CandidateCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var allTypes = new List<Type>
        {
            typeof(CustomAliasedMessage),
            typeof(SimpleBenchmarkMessage),
            typeof(OrderCreatedBenchmarkMessage),
            typeof(LargePayloadBenchmarkMessage),
            typeof(BenchmarkMessage1),
            typeof(BenchmarkMessage2),
            typeof(BenchmarkMessage3),
            typeof(BenchmarkMessage4),
            typeof(BenchmarkMessage5),
            typeof(BenchmarkMessage6),
            typeof(BenchmarkMessage7),
            typeof(BenchmarkMessage8),
            typeof(BenchmarkMessage9),
            typeof(BenchmarkMessage10),
            typeof(BenchmarkMessage11),
            typeof(BenchmarkMessage12),
            typeof(BenchmarkMessage13),
            typeof(BenchmarkMessage14),
            typeof(BenchmarkMessage15),
            typeof(BenchmarkMessage16)
        };

        _candidates = allTypes.Take(CandidateCount).ToArray();

        _simpleName = nameof(SimpleBenchmarkMessage);
        _fullName = typeof(SimpleBenchmarkMessage).FullName!;
        _customAlias = "custom.benchmark.order";
        _assemblyQualifiedName = typeof(SimpleBenchmarkMessage).AssemblyQualifiedName!;
        _urnName = $"urn:message:{typeof(SimpleBenchmarkMessage).Namespace}:{nameof(SimpleBenchmarkMessage)}";
    }

    [Benchmark]
    public string GetTypeIdentifier_SimpleType() =>
        _resolver.GetTypeIdentifier(typeof(SimpleBenchmarkMessage));

    [Benchmark]
    public string GetTypeIdentifier_CustomAlias() =>
        _resolver.GetTypeIdentifier(typeof(CustomAliasedMessage));

    [Benchmark]
    public Type? ResolveType_SimpleName() =>
        _resolver.ResolveType(_simpleName, _candidates);

    [Benchmark]
    public Type? ResolveType_FullName() =>
        _resolver.ResolveType(_fullName, _candidates);

    [Benchmark]
    public Type? ResolveType_CustomAlias() =>
        _resolver.ResolveType(_customAlias, _candidates);

    [Benchmark]
    public Type? ResolveType_AssemblyQualifiedName() =>
        _resolver.ResolveType(_assemblyQualifiedName, _candidates);

    [Benchmark]
    public Type? ResolveType_UrnFormat() =>
        _resolver.ResolveType(_urnName, _candidates);

    [Benchmark]
    public Type? ResolveType_NullIdentifier() =>
        _resolver.ResolveType(null, _candidates);
}
