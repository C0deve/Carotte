using System.Text.RegularExpressions;

namespace Carotte;

internal static partial class StringExtensions
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

        public string ToConsumerExchangeName(string? clientName = null)
        {
            var kebabName = name.ToKebabCase();
            return string.IsNullOrEmpty(clientName)
                ? $"x.sub.{kebabName}"
                : $"x.sub.{clientName.ToKebabCase()}.{kebabName}";
        }

        public string ToConsumerQueueName(string? clientName = null)
        {
            var kebabName = name.ToKebabCase();
            return string.IsNullOrEmpty(clientName)
                ? $"q.{kebabName}"
                : $"q.{clientName.ToKebabCase()}.{kebabName}";
        }

        public string ToDefaultExchangeName() => name.ToMessageExchangeName();

        public string ToDeadLetterExchangeName()
        {
            var normalizedQueueName = name.NormalizeQueueNameForDeadLetter();
            return $"x.dlx.{normalizedQueueName}";
        }

        public string ToDeadLetterQueueName()
        {
            var normalizedQueueName = name.NormalizeQueueNameForDeadLetter();
            return $"q.dlq.{normalizedQueueName}";
        }

        private string NormalizeQueueNameForDeadLetter()
        {
            var kebabName = name.ToKebabCase();
            return kebabName.StartsWith("q.", StringComparison.OrdinalIgnoreCase)
                ? kebabName[2..]
                : kebabName;
        }
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex KebabCaseRegex();
}
