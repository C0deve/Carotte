using System.Text;

namespace Carotte.Documentation;

public sealed class ConsumerDocumenter : IConsumerDocumenter
{
    public string Generate(IReadOnlyCollection<ConsumerInfo> consumers)
    {
        if (consumers.Count == 0)
        {
            return "### Consumed Messages\n\n*No consumed messages configured.*\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### Consumed Messages");
        sb.AppendLine();
        sb.AppendLine("| Message | Consumer | Queue | Broker | Bindings | Error Strategy |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

        foreach (var consumer in consumers.OrderBy(c => c.ConsumerType.Name))
        {
            var messages = string.Join("<br/>", consumer.MessageTypes.Select(m => $"`{m.Name}`"));
            var consumerName = $"`{consumer.ConsumerType.Name}`";
            var queue = $"`{consumer.Topology.Queue}`";
            var broker = string.IsNullOrEmpty(consumer.Broker) ? "-" : $"`{consumer.Broker}`";
            var bindings = FormatBindings(consumer.Topology);
            var errorStrategy = FormatErrorStrategy(consumer.Topology.ErrorStrategy);

            sb.AppendLine($"| {messages} | {consumerName} | {queue} | {broker} | {bindings} | {errorStrategy} |");
        }

        return sb.ToString();
    }

    private static string FormatBindings(IConsumerTopology topology) => topology switch
    {
        ConsumerConventionTopology convention =>
            convention.MessageExchangeNames.Count > 0
                ? string.Join("<br/>", convention.MessageExchangeNames.Select(me => $"`{me}` &rarr; `{convention.ConsumerExchangeName}`"))
                : $"`{convention.ConsumerExchangeName}`",
        ConsumerAttributeTopology attribute =>
            attribute.Bindings.Count > 0
                ? string.Join("<br/>", attribute.Bindings
                    .Where(b => !string.IsNullOrEmpty(b.ExchangeSource) || !string.IsNullOrEmpty(b.RoutingKey))
                    .Select(b => string.IsNullOrEmpty(b.RoutingKey)
                        ? $"`{b.ExchangeSource}` ({b.ExchangeType})"
                        : $"`{b.ExchangeSource}` (key: `{b.RoutingKey}`, {b.ExchangeType})"))
                : "-",
        _ => "-"
    };

    private static string FormatErrorStrategy(ConsumerErrorStrategy strategy)
    {
        var parts = new List<string>();

        if (strategy.MaxRetryAttempts.HasValue)
        {
            parts.Add($"{strategy.MaxRetryAttempts.Value} retries");
        }
        else
        {
            parts.Add($"{strategy.EffectiveMaxRetryAttempts} retries (default)");
        }

        if (strategy.RequeueOnFailure)
        {
            parts.Add("Requeue on fail");
        }
        else if (!string.IsNullOrEmpty(strategy.DeadLetterExchange))
        {
            parts.Add($"DLX: `{strategy.DeadLetterExchange}`");
        }

        if (strategy.InitialRetryInterval.HasValue && strategy.InitialRetryInterval.Value > TimeSpan.Zero)
        {
            parts.Add($"Delay: {strategy.InitialRetryInterval.Value.TotalSeconds}s");
        }

        return string.Join(", ", parts);
    }
}
