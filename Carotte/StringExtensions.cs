using System.Text.RegularExpressions;

namespace Carotte;

public static partial class StringExtensions
{
    public static string ToDefaultQueueName(this string name)
    {
        var result = KebabCaseRegex().Replace(name, "$1-$2").ToLower();
        return $"{result}-queue";
    }

    public static string ToDefaultExchangeName(this string name)
    {
        var result = name;
        if (result.EndsWith("Message", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(0, result.Length - "Message".Length);
        }
        
        result = KebabCaseRegex().Replace(result, "$1-$2").ToLower();
        return $"message-{result}";
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex KebabCaseRegex();
}
