using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers;

namespace Carotte.Documentation.AsyncApi;

public interface IAsyncApiDocumentValidator
{
    AsyncApiValidationResult Validate(string content);
}

public sealed record AsyncApiValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public sealed class AsyncApiDocumentValidator(AsyncApiReaderSettings? settings = null) : IAsyncApiDocumentValidator
{
    private readonly AsyncApiReaderSettings _settings = settings ?? new AsyncApiReaderSettings
    {
        Bindings = BindingsCollection.All
    };

    public AsyncApiValidationResult Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new AsyncApiValidationResult(false, ["Content is empty."], []);
        }

        var reader = new AsyncApiStringReader(_settings);
        var document = reader.Read(content, out var diagnostic);

        var errors = diagnostic?.Errors?.Select(e => e.Message).ToList() ?? [];
        var warnings = diagnostic?.Warnings?.Select(w => w.Message).ToList() ?? [];

        var isValid = document != null && errors.Count == 0;

        return new AsyncApiValidationResult(isValid, errors, warnings);
    }
}
