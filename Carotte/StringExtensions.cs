using System.Text.RegularExpressions;

namespace Carotte;

public static partial class StringExtensions
{
    extension(string name)
    {
        private string ToKebabCase() =>
            KebabCaseRegex().Replace(name, "$1-$2").ToLower();

        private string CleanMessageSuffix()
        {
            var result = name;
            if (result.EndsWith("Message", StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^"Message".Length];
            }

            if (result.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^"Event".Length];
            }

            if (result.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^"Command".Length];
            }

            return result;
        }

        public string ToMessageExchangeName()
        {
            var cleaned = name.CleanMessageSuffix();
            return $"x.pub.{cleaned.ToKebabCase()}";
        }

        public string ToConsumerExchangeName() => $"x.sub.{name.ToKebabCase()}";

        public string ToConsumerQueueName() => $"q.{name.ToKebabCase()}";

        public string ToDefaultQueueName() => name.ToConsumerQueueName();

        public string ToDefaultExchangeName() => name.ToMessageExchangeName();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex KebabCaseRegex();
}