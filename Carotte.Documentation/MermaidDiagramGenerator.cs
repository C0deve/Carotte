using System.Text;
using System.Text.RegularExpressions;

namespace Carotte.Documentation;

public sealed partial class MermaidDiagramGenerator : IMermaidDiagramGenerator
{
    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex SanitizeIdentifierRegex();

    public string Generate(MessageBrokerSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Messaging Topology");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");

        var hasProducers = settings.Producers.Count > 0;
        var hasConsumers = settings.Consumers.Count > 0;

        if (!hasProducers && !hasConsumers)
        {
            sb.AppendLine("    %% No producers or consumers configured");
            sb.AppendLine("```");
            sb.AppendLine();
            return sb.ToString();
        }

        var declaredNodes = new HashSet<string>(StringComparer.Ordinal);
        var declaredLinks = new HashSet<string>(StringComparer.Ordinal);

        // Subgraph for Microservice components
        sb.AppendLine("    subgraph Microservice");
        foreach (var producer in settings.Producers.OrderBy(p => p.MessageType.Name))
        {
            var pubId = $"{Sanitize(producer.MessageType.Name)}_Publisher";
            sb.AppendLine($"        {pubId}[\"{producer.MessageType.Name} Publisher\"]");
            declaredNodes.Add(pubId);
        }

        foreach (var consumer in settings.Consumers.OrderBy(c => c.ConsumerType.Name))
        {
            var consId = Sanitize(consumer.ConsumerType.Name);
            sb.AppendLine($"        {consId}[\"{consumer.ConsumerType.Name}\"]");
            declaredNodes.Add(consId);
        }
        sb.AppendLine("    end");
        sb.AppendLine();

        // Producer connections to Exchanges
        foreach (var producer in settings.Producers.OrderBy(p => p.MessageType.Name))
        {
            var pubId = $"{Sanitize(producer.MessageType.Name)}_Publisher";
            var exchId = $"exch_{Sanitize(producer.ExchangePublication)}";

            if (declaredNodes.Add(exchId))
            {
                sb.AppendLine($"    {exchId}[(\"{producer.ExchangePublication}\")]");
            }

            var link = string.IsNullOrEmpty(producer.RoutingKey)
                ? $"    {pubId} --> {exchId}"
                : $"    {pubId} -->|\"{producer.RoutingKey}\"| {exchId}";

            if (declaredLinks.Add(link))
            {
                sb.AppendLine(link);
            }
        }

        sb.AppendLine();

        // Consumer connections from Exchanges / Queues to Consumers
        foreach (var consumer in settings.Consumers.OrderBy(c => c.ConsumerType.Name))
        {
            var consId = Sanitize(consumer.ConsumerType.Name);
            var queueId = $"queue_{Sanitize(consumer.Topology.Queue)}";

            if (declaredNodes.Add(queueId))
            {
                sb.AppendLine($"    {queueId}[[\"{consumer.Topology.Queue}\"]]");
            }

            var queueToConsLink = $"    {queueId} --> {consId}";
            if (declaredLinks.Add(queueToConsLink))
            {
                sb.AppendLine(queueToConsLink);
            }

            if (consumer.Topology is ConsumerConventionTopology convention)
            {
                var consExchId = $"exch_{Sanitize(convention.ConsumerExchangeName)}";
                if (declaredNodes.Add(consExchId))
                {
                    sb.AppendLine($"    {consExchId}[(\"{convention.ConsumerExchangeName}\")]");
                }

                var exchToQueueLink = $"    {consExchId} --> {queueId}";
                if (declaredLinks.Add(exchToQueueLink))
                {
                    sb.AppendLine(exchToQueueLink);
                }

                foreach (var msgExch in convention.MessageExchangeNames)
                {
                    var msgExchId = $"exch_{Sanitize(msgExch)}";
                    if (declaredNodes.Add(msgExchId))
                    {
                        sb.AppendLine($"    {msgExchId}[(\"{msgExch}\")]");
                    }

                    var msgToConsExchLink = $"    {msgExchId} --> {consExchId}";
                    if (declaredLinks.Add(msgToConsExchLink))
                    {
                        sb.AppendLine(msgToConsExchLink);
                    }
                }
            }
            else if (consumer.Topology is ConsumerAttributeTopology attribute)
            {
                foreach (var binding in attribute.Bindings.Where(b => !string.IsNullOrEmpty(b.ExchangeSource)))
                {
                    var exchId = $"exch_{Sanitize(binding.ExchangeSource)}";
                    if (declaredNodes.Add(exchId))
                    {
                        sb.AppendLine($"    {exchId}[(\"{binding.ExchangeSource}\")]");
                    }

                    var bindingLink = string.IsNullOrEmpty(binding.RoutingKey)
                        ? $"    {exchId} --> {queueId}"
                        : $"    {exchId} -->|\"{binding.RoutingKey}\"| {queueId}";

                    if (declaredLinks.Add(bindingLink))
                    {
                        sb.AppendLine(bindingLink);
                    }
                }
            }

            // Dead letter exchange if configured
            var errorStrategy = consumer.Topology.ErrorStrategy;
            if (!string.IsNullOrEmpty(errorStrategy.DeadLetterExchange))
            {
                var dlxId = $"dlx_{Sanitize(errorStrategy.DeadLetterExchange)}";
                if (declaredNodes.Add(dlxId))
                {
                    sb.AppendLine($"    {dlxId}[(\"{errorStrategy.DeadLetterExchange}\")]");
                }

                var dlxLink = $"    {queueId} -.->|\"DLX\"| {dlxId}";
                if (declaredLinks.Add(dlxLink))
                {
                    sb.AppendLine(dlxLink);
                }

                if (!string.IsNullOrEmpty(errorStrategy.DeadLetterQueue))
                {
                    var dlqId = $"dlq_{Sanitize(errorStrategy.DeadLetterQueue)}";
                    if (declaredNodes.Add(dlqId))
                    {
                        sb.AppendLine($"    {dlqId}[[\"{errorStrategy.DeadLetterQueue}\"]]");
                    }

                    var dlqLink = $"    {dlxId} --> {dlqId}";
                    if (declaredLinks.Add(dlqLink))
                    {
                        sb.AppendLine(dlqLink);
                    }
                }
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Sanitize(string name) =>
        SanitizeIdentifierRegex().Replace(name, "_");
}
