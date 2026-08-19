using Shouldly;
using Carotte.Documentation.AsyncApi;

namespace Carotte.Documentation.Tests;

public class AsyncApiSerializerTests
{
    private readonly JsonAsyncApiSerializer _jsonSerializer = new();
    private readonly YamlAsyncApiSerializer _yamlSerializer = new();

    private static AsyncApiDocument CreateSampleDocument()
    {
        return new AsyncApiDocument
        {
            AsyncApi = "2.6.0",
            Info = new AsyncApiInfo
            {
                Title = "Test Messaging Service",
                Version = "1.0.0",
                Description = "Sample AsyncAPI documentation"
            },
            Servers = new Dictionary<string, AsyncApiServer>
            {
                ["primary-broker"] = new AsyncApiServer
                {
                    Url = "localhost:5672",
                    Protocol = "amqp",
                    ProtocolVersion = "0.9.1",
                    Description = "Primary RabbitMQ broker"
                }
            },
            Channels = new Dictionary<string, AsyncApiChannel>
            {
                ["orders.exchange/order.created"] = new AsyncApiChannel
                {
                    Publish = new AsyncApiOperation
                    {
                        OperationId = "publishOrderCreated",
                        Summary = "Publishes OrderCreated event",
                        Message = new AsyncApiMessageRef { Ref = "#/components/messages/OrderCreated" }
                    }
                }
            },
            Components = new AsyncApiComponents
            {
                Messages = new Dictionary<string, AsyncApiMessage>
                {
                    ["OrderCreated"] = new AsyncApiMessage
                    {
                        Name = "OrderCreated",
                        Title = "OrderCreated",
                        Summary = "Order created event",
                        Payload = new AsyncApiSchemaRef { Ref = "#/components/schemas/OrderCreated" }
                    }
                },
                Schemas = new Dictionary<string, AsyncApiSchema>
                {
                    ["OrderCreated"] = new AsyncApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, AsyncApiSchema>
                        {
                            ["OrderId"] = new AsyncApiSchema { Type = "string", Format = "uuid" },
                            ["Amount"] = new AsyncApiSchema { Type = "number", Format = "double" }
                        },
                        Required = ["OrderId", "Amount"]
                    }
                }
            }
        };
    }

    [Fact]
    public void Serialize_ToJson_ShouldProduceValidJson()
    {
        // Arrange
        var doc = CreateSampleDocument();

        // Act
        var json = _jsonSerializer.Serialize(doc);

        // Assert
        json.ShouldContain("\"asyncapi\": \"2.6.0\"");
        json.ShouldContain("\"title\": \"Test Messaging Service\"");
        json.ShouldContain("\"localhost:5672\"");
        json.ShouldContain("\"$ref\": \"#/components/messages/OrderCreated\"");
    }

    [Fact]
    public void Serialize_ToYaml_ShouldProduceValidYaml()
    {
        // Arrange
        var doc = CreateSampleDocument();

        // Act
        var yaml = _yamlSerializer.Serialize(doc);

        // Assert
        yaml.ShouldContain("asyncapi: 2.6.0");
        yaml.ShouldContain("title: Test Messaging Service");
        yaml.ShouldContain("url: localhost:5672");
        yaml.ShouldContain("$ref: '#/components/messages/OrderCreated'");
    }

    [Fact]
    public void Serialize_ToYaml_ShouldFormatListItemsCorrectly()
    {
        // Arrange
        var doc = CreateSampleDocument();

        // Act
        var yaml = _yamlSerializer.Serialize(doc);

        // Assert
        yaml.ShouldContain("required:");
        yaml.ShouldContain("  - OrderId");
        yaml.ShouldContain("  - Amount");
    }
}
