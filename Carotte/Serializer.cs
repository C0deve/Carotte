using System.Text.Json;

namespace Carotte;

public interface ISerializer
{
    byte[] Serialize<T>(T message);
    T? Deserialize<T>(byte[] data);
}

public class JsonSerializerImpl(JsonSerializerOptions? options = null) : ISerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JsonSerializerOptions _options = options ?? DefaultOptions;

    public byte[] Serialize<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    public T? Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, _options);
    }
}
