namespace Carotte.Documentation;

public interface IConsumerDocumenter
{
    string Generate(IReadOnlyCollection<ConsumerInfo> consumers);
}
