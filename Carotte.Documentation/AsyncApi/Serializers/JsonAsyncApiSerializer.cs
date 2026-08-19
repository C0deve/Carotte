using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carotte.Documentation.AsyncApi;

public sealed class JsonAsyncApiSerializer : IAsyncApiSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Serialize(AsyncApiDocument document) =>
        JsonSerializer.Serialize(document, s_options);
}
