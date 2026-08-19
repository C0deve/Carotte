using System.Collections;
using System.Reflection;

namespace Carotte.Documentation.AsyncApi;

public sealed class JsonSchemaGenerator : IJsonSchemaGenerator
{
    public AsyncApiSchema Generate(Type type, IXmlDocumentationReader? xmlReader = null)
    {
        return GenerateInternal(type, xmlReader, new HashSet<Type>());
    }

    private AsyncApiSchema GenerateInternal(
        Type type,
        IXmlDocumentationReader? xmlReader,
        HashSet<Type> visitedTypes)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying != null)
        {
            var innerSchema = GenerateInternal(nullableUnderlying, xmlReader, visitedTypes);
            return innerSchema with { Nullable = true };
        }

        if (type == typeof(string))
        {
            return new AsyncApiSchema { Type = "string" };
        }

        if (type == typeof(int) || type == typeof(short) || type == typeof(byte))
        {
            return new AsyncApiSchema { Type = "integer", Format = "int32" };
        }

        if (type == typeof(long))
        {
            return new AsyncApiSchema { Type = "integer", Format = "int64" };
        }

        if (type == typeof(float))
        {
            return new AsyncApiSchema { Type = "number", Format = "float" };
        }

        if (type == typeof(double) || type == typeof(decimal))
        {
            return new AsyncApiSchema { Type = "number", Format = "double" };
        }

        if (type == typeof(bool))
        {
            return new AsyncApiSchema { Type = "boolean" };
        }

        if (type == typeof(Guid))
        {
            return new AsyncApiSchema { Type = "string", Format = "uuid" };
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return new AsyncApiSchema { Type = "string", Format = "date-time" };
        }

        if (type == typeof(TimeSpan))
        {
            return new AsyncApiSchema { Type = "string", Format = "duration" };
        }

        if (type == typeof(Uri))
        {
            return new AsyncApiSchema { Type = "string", Format = "uri" };
        }

        if (type.IsEnum)
        {
            return new AsyncApiSchema
            {
                Type = "string",
                EnumValues = [.. Enum.GetNames(type)]
            };
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType() ?? typeof(object);
            return new AsyncApiSchema
            {
                Type = "array",
                Items = GenerateInternal(elementType, xmlReader, visitedTypes)
            };
        }

        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var itemType = GetEnumerableItemType(type);
            return new AsyncApiSchema
            {
                Type = "array",
                Items = GenerateInternal(itemType, xmlReader, visitedTypes)
            };
        }

        if (visitedTypes.Contains(type))
        {
            return new AsyncApiSchema { Ref = $"#/components/schemas/{type.Name}" };
        }

        var newVisited = new HashSet<Type>(visitedTypes) { type };
        var properties = new Dictionary<string, AsyncApiSchema>();
        var requiredList = new List<string>();

        var typeSummary = xmlReader?.GetTypeSummary(type);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propSchema = GenerateInternal(property.PropertyType, xmlReader, newVisited);
            var propSummary = xmlReader?.GetPropertySummary(type, property.Name);

            if (!string.IsNullOrWhiteSpace(propSummary))
            {
                propSchema = propSchema with { Description = propSummary };
            }

            properties[property.Name] = propSchema;

            if (IsRequiredProperty(property))
            {
                requiredList.Add(property.Name);
            }
        }

        return new AsyncApiSchema
        {
            Type = "object",
            Description = string.IsNullOrWhiteSpace(typeSummary) ? null : typeSummary,
            Properties = properties.Count > 0 ? properties : null,
            Required = requiredList.Count > 0 ? requiredList : null
        };
    }

    private static Type GetEnumerableItemType(Type type)
    {
        if (type.IsGenericType && type.GetGenericArguments().Length == 1)
        {
            return type.GetGenericArguments()[0];
        }

        var enumInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumInterface?.GetGenericArguments()[0] ?? typeof(object);
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
        {
            return true;
        }

        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);
        return nullabilityInfo.WriteState == NullabilityState.NotNull;
    }
}
