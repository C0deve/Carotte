
using System.Collections.ObjectModel;

namespace Carotte;

public interface IConsumerTopology
{
    string Queue { get; }
    string Broker { get; }
    ushort PrefetchCount { get; }
}

public record ConsumerConventionTopology(
    string Broker,
    string Queue,
    string ConsumerExchangeName,
    ReadOnlyCollection<string> MessageExchangeNames,
    ushort PrefetchCount = 1) : IConsumerTopology;

public record ConsumerAttributeTopology(
    string Broker,
    string Queue,
    ReadOnlyCollection<BindingInfo> Bindings,
    ushort PrefetchCount = 1) : IConsumerTopology;

public readonly record struct ConsumerInfo(
    Type ConsumerType,
    HashSet<Type> MessageTypes,
    string Broker,
    IConsumerTopology Topology
);
public readonly record struct BrokerInfos(string Host, int Port, string UserName, string Password)
{
    public static BrokerInfos Default => new("localhost", 5672, "guest", "guest");
}

public readonly record struct BindingInfo(
    string ExchangeSource,
    string RoutingKey);

internal readonly record struct ProducerInfo(
    Type MessageType,
    string Broker,
    string ExchangePublication);

