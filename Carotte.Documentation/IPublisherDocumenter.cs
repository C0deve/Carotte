namespace Carotte.Documentation;

public interface IPublisherDocumenter
{
    string Generate(IReadOnlyCollection<PublisherInfo> publishers);
}
