using System.Text;

namespace Carotte.Documentation;

public sealed class ProducerDocumenter : IProducerDocumenter
{
    public string Generate(IReadOnlyCollection<ProducerInfo> producers)
    {
        if (producers.Count == 0)
        {
            return "### Produced Messages\n\n*No produced messages configured.*\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### Produced Messages");
        sb.AppendLine();
        sb.AppendLine("| Message | Broker | Exchange | Routing Key | Exchange Type |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var producer in producers.OrderBy(p => p.MessageType.Name))
        {
            var routingKey = string.IsNullOrEmpty(producer.RoutingKey) ? "-" : $"`{producer.RoutingKey}`";
            var exchange = string.IsNullOrEmpty(producer.ExchangePublication) ? "-" : $"`{producer.ExchangePublication}`";
            var broker = string.IsNullOrEmpty(producer.Broker) ? "-" : $"`{producer.Broker}`";

            sb.AppendLine($"| `{producer.MessageType.Name}` | {broker} | {exchange} | {routingKey} | `{producer.ExchangeType}` |");
        }

        return sb.ToString();
    }
}
