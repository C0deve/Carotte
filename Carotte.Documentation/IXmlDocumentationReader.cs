namespace Carotte.Documentation;

public interface IXmlDocumentationReader
{
    string? GetTypeSummary(Type type);
    string? GetPropertySummary(Type type, string propertyName);
}
