namespace Carotte.Documentation;

public interface IDataContractDocumenter
{
    string Generate(IReadOnlyCollection<Type> messageTypes, IXmlDocumentationReader? xmlReader = null);
}
