using System.Text;

namespace Carotte.Documentation;

public sealed class PublisherDocumenter : IPublisherDocumenter
{
    public string Generate(IReadOnlyCollection<PublisherInfo> publishers)
    {
        if (publishers.Count == 0)
        {
            return "### Produced Messages\n\n*No produced messages configured.*\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### Produced Messages");
        sb.AppendLine();
        sb.AppendLine("| Message | Broker | Exchange | Routing Key | Exchange Type |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var publisher in publishers.OrderBy(p => p.MessageType.Name))
        {
            var routingKey = string.IsNullOrEmpty(publisher.RoutingKey) ? "-" : $"`{publisher.RoutingKey}`";
            var exchange = string.IsNullOrEmpty(publisher.ExchangePublication) ? "-" : $"`{publisher.ExchangePublication}`";
            var broker = string.IsNullOrEmpty(publisher.Broker) ? "-" : $"`{publisher.Broker}`";

            sb.AppendLine($"| `{publisher.MessageType.Name}` | {broker} | {exchange} | {routingKey} | `{publisher.ExchangeType}` |");
        }

        return sb.ToString();
    }
}
