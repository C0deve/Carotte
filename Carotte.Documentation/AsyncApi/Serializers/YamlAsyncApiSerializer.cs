using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carotte.Documentation.AsyncApi;

public sealed class YamlAsyncApiSerializer : IAsyncApiSerializer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Serialize(AsyncApiDocument document)
    {
        var element = JsonSerializer.SerializeToElement(document, s_jsonOptions);
        var sb = new StringBuilder();
        WriteElement(element, sb, 0);
        return sb.ToString();
    }

    private static void WriteElement(JsonElement element, StringBuilder sb, int indent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, sb, indent);
                break;
            case JsonValueKind.Array:
                WriteArray(element, sb, indent);
                break;
            default:
                sb.Append(' ', indent).Append(FormatScalar(element)).Append('\n');
                break;
        }
    }

    private static void WriteObject(JsonElement obj, StringBuilder sb, int indent)
    {
        var properties = obj.EnumerateObject().ToList();
        if (properties.Count == 0)
        {
            sb.Append("{}\n");
            return;
        }

        foreach (var prop in properties)
        {
            var value = prop.Value;
            var key = FormatKey(prop.Name);

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var innerProps = value.EnumerateObject().ToList();
                    if (innerProps.Count == 0)
                    {
                        sb.Append(' ', indent).Append(key).Append(": {}\n");
                    }
                    else
                    {
                        sb.Append(' ', indent).Append(key).Append(":\n");
                        WriteObject(value, sb, indent + 2);
                    }
                    break;

                case JsonValueKind.Array:
                    var innerItems = value.EnumerateArray().ToList();
                    if (innerItems.Count == 0)
                    {
                        sb.Append(' ', indent).Append(key).Append(": []\n");
                    }
                    else
                    {
                        sb.Append(' ', indent).Append(key).Append(":\n");
                        WriteArray(value, sb, indent + 2);
                    }
                    break;

                default:
                    sb.Append(' ', indent).Append(key).Append(": ").Append(FormatScalar(value)).Append('\n');
                    break;
            }
        }
    }

    private static void WriteArray(JsonElement array, StringBuilder sb, int indent)
    {
        var items = array.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            sb.Append("[]\n");
            return;
        }

        foreach (var item in items)
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.Object:
                    var objProps = item.EnumerateObject().ToList();
                    if (objProps.Count == 0)
                    {
                        sb.Append(' ', indent).Append("- {}\n");
                    }
                    else
                    {
                        var first = objProps[0];
                        var firstKey = FormatKey(first.Name);
                        if (first.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        {
                            sb.Append(' ', indent).Append("- ").Append(firstKey).Append(":\n");
                            WriteElement(first.Value, sb, indent + 4);
                        }
                        else
                        {
                            sb.Append(' ', indent).Append("- ").Append(firstKey).Append(": ").Append(FormatScalar(first.Value)).Append('\n');
                        }

                        for (var i = 1; i < objProps.Count; i++)
                        {
                            var prop = objProps[i];
                            var propKey = FormatKey(prop.Name);
                            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            {
                                sb.Append(' ', indent + 2).Append(propKey).Append(":\n");
                                WriteElement(prop.Value, sb, indent + 4);
                            }
                            else
                            {
                                sb.Append(' ', indent + 2).Append(propKey).Append(": ").Append(FormatScalar(prop.Value)).Append('\n');
                            }
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    sb.Append(' ', indent).Append("-\n");
                    WriteArray(item, sb, indent + 2);
                    break;

                default:
                    sb.Append(' ', indent).Append("- ").Append(FormatScalar(item)).Append('\n');
                    break;
            }
        }
    }

    private static string FormatKey(string key)
    {
        if (NeedsQuoting(key))
        {
            return $"'{key.Replace("'", "''")}'";
        }
        return key;
    }

    private static string FormatScalar(JsonElement scalar)
    {
        switch (scalar.ValueKind)
        {
            case JsonValueKind.String:
                var str = scalar.GetString() ?? "";
                if (NeedsQuoting(str))
                {
                    return $"'{str.Replace("'", "''")}'";
                }
                return str;

            case JsonValueKind.Number:
                return scalar.GetRawText();

            case JsonValueKind.True:
                return "true";

            case JsonValueKind.False:
                return "false";

            case JsonValueKind.Null:
                return "null";

            default:
                return scalar.GetRawText();
        }
    }

    private static bool NeedsQuoting(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return true;
        }

        if (str.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            str.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (str.StartsWith('#') || str.StartsWith('@') || str.StartsWith('`') || str.StartsWith('&') ||
            str.StartsWith('*') || str.StartsWith('!') || str.StartsWith('|') || str.StartsWith('>') ||
            str.StartsWith('%') || str.StartsWith('?') || str.StartsWith('{') || str.StartsWith('[') ||
            str.StartsWith('-') || str.StartsWith(':') || str.StartsWith('\'') || str.StartsWith('"'))
        {
            return true;
        }

        if (str.Contains(": ") || str.Contains('#') || str.Contains('\n') || str.Contains('\r') ||
            str.Contains('{') || str.Contains('}') || str.Contains('[') || str.Contains(']'))
        {
            return true;
        }

        return false;
    }
}
