using System.Reflection;
using System.Text;

namespace Carotte.Documentation;

public sealed class DataContractDocumenter : IDataContractDocumenter
{
    public string Generate(IReadOnlyCollection<Type> messageTypes, IXmlDocumentationReader? xmlReader = null)
    {
        if (messageTypes.Count == 0)
        {
            return "### Data Contracts\n\n*No data contracts found.*\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### Data Contracts");
        sb.AppendLine();

        foreach (var messageType in messageTypes.Distinct().OrderBy(t => t.Name))
        {
            sb.AppendLine($"#### `{messageType.Name}`");
            sb.AppendLine();

            var typeSummary = xmlReader?.GetTypeSummary(messageType);
            if (!string.IsNullOrWhiteSpace(typeSummary))
            {
                sb.AppendLine($"*{typeSummary}*");
                sb.AppendLine();
            }

            var properties = messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length == 0)
            {
                sb.AppendLine("*Empty message contract (no public properties).*\n");
                continue;
            }

            sb.AppendLine("| Property | Type | Description |");
            sb.AppendLine("| :--- | :--- | :--- |");

            foreach (var prop in properties)
            {
                var typeName = FormatTypeName(prop.PropertyType);
                var propSummary = xmlReader?.GetPropertySummary(messageType, prop.Name) ?? "-";
                sb.AppendLine($"| `{prop.Name}` | `{typeName}` | {propSummary} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatTypeName(Type type)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying != null)
        {
            return $"{FormatTypeName(nullableUnderlying)}?";
        }

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(object)) return "object";
        if (type == typeof(Guid)) return "Guid";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
        if (type == typeof(TimeSpan)) return "TimeSpan";
        if (type == typeof(Uri)) return "Uri";

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            var cleanName = genericTypeDef.Name;
            var backtickIndex = cleanName.IndexOf('`');
            if (backtickIndex > 0)
            {
                cleanName = cleanName[..backtickIndex];
            }
            return $"{cleanName}<{genericArgs}>";
        }

        return type.Name;
    }
}
