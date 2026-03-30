using Shouldly;

namespace Carotte.Tests;

public class SerializerTests
{
    private readonly ISerializer _serializer = new JsonSerializerImpl();

    [Fact]
    public void Serialize_ShouldReturnBytes()
    {
        var message = new TestMessage("Hello World");
        var result = _serializer.Serialize(message);
        
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Deserialize_ShouldReturnOriginalObject()
    {
        var original = new TestMessage("Test Content");
        var bytes = _serializer.Serialize(original);
        
        var deserialized = _serializer.Deserialize<TestMessage>(bytes);
        
        deserialized.ShouldNotBeNull();
        deserialized.Content.ShouldBe(original.Content);
    }
}
