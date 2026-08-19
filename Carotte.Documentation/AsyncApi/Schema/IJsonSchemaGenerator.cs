namespace Carotte.Documentation.AsyncApi;

public interface IJsonSchemaGenerator
{
    AsyncApiSchema Generate(Type type, IXmlDocumentationReader? xmlReader = null);
}
