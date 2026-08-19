namespace Carotte.Documentation;

public interface IProducerDocumenter
{
    string Generate(IReadOnlyCollection<ProducerInfo> producers);
}
