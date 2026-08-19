namespace Carotte.Documentation.AsyncApi;

public interface IAsyncApiSerializer
{
    string Serialize(AsyncApiDocument document);
}
