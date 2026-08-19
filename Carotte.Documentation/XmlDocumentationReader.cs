using System.Xml.Linq;

namespace Carotte.Documentation;

public sealed class XmlDocumentationReader : IXmlDocumentationReader
{
    private readonly Dictionary<string, string> _summaries;

    private XmlDocumentationReader(Dictionary<string, string> summaries) =>
        _summaries = summaries;

    public static XmlDocumentationReader Empty => new(new Dictionary<string, string>());

    public static XmlDocumentationReader? FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(filePath);
            return FromXmlString(content);
        }
        catch
        {
            return null;
        }
    }

    public static XmlDocumentationReader FromXmlString(string xmlContent)
    {
        var summaries = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var members = doc.Root?.Element("members")?.Elements("member");
            if (members != null)
            {
                foreach (var member in members)
                {
                    var name = member.Attribute("name")?.Value;
                    var summary = member.Element("summary")?.Value.Trim();
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(summary))
                    {
                        summaries[name] = summary;
                    }
                }
            }
        }
        catch
        {
            // Ignore XML parse errors gracefully
        }

        return new XmlDocumentationReader(summaries);
    }

    public string? GetTypeSummary(Type type)
    {
        var key = $"T:{type.FullName}";
        return _summaries.GetValueOrDefault(key);
    }

    public string? GetPropertySummary(Type type, string propertyName)
    {
        var key = $"P:{type.FullName}.{propertyName}";
        return _summaries.GetValueOrDefault(key);
    }
}
