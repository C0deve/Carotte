using System.Text.Json;

namespace Carotte;

public interface ISerializer
{
    byte[] Serialize<T>(T message);
    T? Deserialize<T>(byte[] data);
}

public class JsonSerializerImpl : ISerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public byte[] Serialize<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, Options);
    }

    public T? Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, Options);
    }
}
