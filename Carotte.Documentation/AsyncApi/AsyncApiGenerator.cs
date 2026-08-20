using System.Reflection;

namespace Carotte.Documentation.AsyncApi;

public sealed class AsyncApiGenerator(
    IJsonSchemaGenerator? jsonSchemaGenerator = null,
    IAsyncApiSerializer? jsonSerializer = null,
    IAsyncApiSerializer? yamlSerializer = null) : IAsyncApiGenerator
{
    private readonly IJsonSchemaGenerator _jsonSchemaGenerator = jsonSchemaGenerator ?? new JsonSchemaGenerator();
    private readonly IAsyncApiSerializer _jsonSerializer = jsonSerializer ?? new JsonAsyncApiSerializer();
    private readonly IAsyncApiSerializer _yamlSerializer = yamlSerializer ?? new YamlAsyncApiSerializer();

    public string Generate(Assembly assembly, CarotteAsyncApiOptions? options = null) =>
        Generate([assembly], options);

    public string Generate(IReadOnlyCollection<Assembly> assemblies, CarotteAsyncApiOptions? options = null)
    {
        options ??= new CarotteAsyncApiOptions();
        var assemblySet = assemblies.ToHashSet();
        var (consumerScanResults, publisherScanResults) = assemblySet.Scan(options.Namespaces);

        var brokers = options.Brokers ?? new Dictionary<string, RabbitMqOptions>(StringComparer.OrdinalIgnoreCase);
        var settings = TopologyProvider.CreateSettings(
            brokers,
            consumerScanResults,
            publisherScanResults,
            options.ClientName,
            options.ConsumerSettings);

        var xmlReader = ResolveXmlReader(assemblies, options.XmlDocumentationPath);
        return GenerateInternal(settings, options, xmlReader);
    }

    public string Generate(MessageBrokerSettings settings, CarotteAsyncApiOptions? options = null) =>
        GenerateInternal(settings, options ?? new CarotteAsyncApiOptions(), null);

    public string Generate(CarotteBuilder builder, CarotteAsyncApiOptions? options = null)
    {
        options ??= new CarotteAsyncApiOptions();
        var (consumerScanResults, publisherScanResults) = builder.Assemblies.Scan(builder.Namespaces);

        var settings = TopologyProvider.CreateSettings(
            builder.Brokers,
            consumerScanResults,
            publisherScanResults,
            builder.ClientName,
            builder.ConsumerSettings);

        var xmlReader = ResolveXmlReader(builder.Assemblies, options.XmlDocumentationPath);
        return GenerateInternal(settings, options, xmlReader);
    }

    public async Task GenerateToFileAsync(
        Assembly assembly,
        string outputPath,
        CarotteAsyncApiOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await GenerateToFileAsync([assembly], outputPath, options, cancellationToken);

    public async Task GenerateToFileAsync(
        IReadOnlyCollection<Assembly> assemblies,
        string outputPath,
        CarotteAsyncApiOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var spec = Generate(assemblies, options);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, spec, cancellationToken);
    }

    private string GenerateInternal(
        MessageBrokerSettings settings,
        CarotteAsyncApiOptions options,
        IXmlDocumentationReader? xmlReader)
    {
        var isV3 = options.SpecVersion is AsyncApiVersion.V3_0 or AsyncApiVersion.V3_1;
        var specVersionString = options.SpecVersion switch
        {
            AsyncApiVersion.V2_6 => "2.6.0",
            AsyncApiVersion.V3_0 => "3.0.0",
            AsyncApiVersion.V3_1 => "3.1.0",
            _ => "3.1.0"
        };

        var servers = BuildServers(settings, isV3);
        var messageTypes = settings.Producers
            .Select(p => p.MessageType)
            .Concat(settings.Consumers.SelectMany(c => c.MessageTypes))
            .Distinct()
            .ToList();

        var (messages, schemas) = BuildComponents(messageTypes, xmlReader);

        var channels = new Dictionary<string, AsyncApiChannel>();
        var operationsV3 = isV3 ? new Dictionary<string, AsyncApiOperationV3>() : null;

        foreach (var producer in settings.Producers)
        {
            var channelKey = string.IsNullOrEmpty(producer.RoutingKey)
                ? producer.ExchangePublication
                : $"{producer.ExchangePublication}/{producer.RoutingKey}";

            if (!channels.TryGetValue(channelKey, out var channel))
            {
                channel = new AsyncApiChannel
                {
                    Address = isV3 ? channelKey : null,
                    Bindings = new AsyncApiChannelBindings
                    {
                        Amqp = new AsyncApiAmqpChannelBinding
                        {
                            Is = "routingKey",
                            Exchange = new AsyncApiAmqpExchangeBinding
                            {
                                Name = producer.ExchangePublication,
                                Type = producer.ExchangeType.ToString().ToLowerInvariant(),
                                Durable = producer.Durable,
                                AutoDelete = producer.AutoDelete
                            }
                        }
                    }
                };
                channels[channelKey] = channel;
            }

            if (isV3)
            {
                channel.Messages ??= new Dictionary<string, AsyncApiMessageRef>();
                channel.Messages[producer.MessageType.Name] = new AsyncApiMessageRef
                {
                    Ref = $"#/components/messages/{producer.MessageType.Name}"
                };

                var opId = $"publish{producer.MessageType.Name}";
                operationsV3![opId] = new AsyncApiOperationV3
                {
                    Action = "send",
                    Summary = $"Publishes {producer.MessageType.Name} message",
                    Channel = new AsyncApiChannelRef { Ref = $"#/channels/{EscapeJsonPointer(channelKey)}" },
                    Messages = [new AsyncApiMessageRef { Ref = $"#/components/messages/{producer.MessageType.Name}" }]
                };
            }
            else
            {
                channels[channelKey] = channel with
                {
                    Publish = new AsyncApiOperation
                    {
                        OperationId = $"publish{producer.MessageType.Name}",
                        Summary = $"Publishes {producer.MessageType.Name} message",
                        Message = new AsyncApiMessageRef
                        {
                            Ref = $"#/components/messages/{producer.MessageType.Name}"
                        },
                        Bindings = new AsyncApiOperationBindings
                        {
                            Amqp = new AsyncApiAmqpOperationBinding
                            {
                                Exchange = new AsyncApiAmqpExchangeBinding
                                {
                                    Name = producer.ExchangePublication,
                                    Type = producer.ExchangeType.ToString().ToLowerInvariant(),
                                    Durable = producer.Durable,
                                    AutoDelete = producer.AutoDelete
                                }
                            }
                        }
                    }
                };
            }
        }

        foreach (var consumer in settings.Consumers)
        {
            var channelKeys = GetConsumerChannelKeys(consumer);

            foreach (var channelKey in channelKeys)
            {
                if (!channels.TryGetValue(channelKey, out var channel))
                {
                    channel = new AsyncApiChannel
                    {
                        Address = isV3 ? channelKey : null
                    };
                    channels[channelKey] = channel;
                }

                var primaryMessageType = consumer.MessageTypes.FirstOrDefault();
                var messageRef = primaryMessageType != null
                    ? new AsyncApiMessageRef { Ref = $"#/components/messages/{primaryMessageType.Name}" }
                    : null;

                if (isV3)
                {
                    if (primaryMessageType != null)
                    {
                        channel.Messages ??= new Dictionary<string, AsyncApiMessageRef>();
                        channel.Messages[primaryMessageType.Name] = messageRef!;
                    }

                    var opId = $"consume{consumer.ConsumerType.Name}";
                    operationsV3![opId] = new AsyncApiOperationV3
                    {
                        Action = "receive",
                        Summary = $"Consumes messages by {consumer.ConsumerType.Name}",
                        Channel = new AsyncApiChannelRef { Ref = $"#/channels/{EscapeJsonPointer(channelKey)}" },
                        Bindings = new AsyncApiOperationBindings
                        {
                            Amqp = new AsyncApiAmqpOperationBinding
                            {
                                Queue = new AsyncApiAmqpQueueBinding
                                {
                                    Name = consumer.Topology.Queue,
                                    Durable = true,
                                    Exclusive = false,
                                    AutoDelete = false
                                }
                            }
                        },
                        Messages = messageRef != null ? [messageRef] : null
                    };
                }
                else
                {
                    channels[channelKey] = channel with
                    {
                        Subscribe = new AsyncApiOperation
                        {
                            OperationId = $"consume{consumer.ConsumerType.Name}",
                            Summary = $"Consumes messages by {consumer.ConsumerType.Name}",
                            Bindings = new AsyncApiOperationBindings
                            {
                                Amqp = new AsyncApiAmqpOperationBinding
                                {
                                    Queue = new AsyncApiAmqpQueueBinding
                                    {
                                        Name = consumer.Topology.Queue,
                                        Durable = true,
                                        Exclusive = false,
                                        AutoDelete = false
                                    }
                                }
                            },
                            Message = messageRef
                        }
                    };
                }
            }
        }

        var document = new AsyncApiDocument
        {
            AsyncApi = specVersionString,
            Info = new AsyncApiInfo
            {
                Title = options.Title,
                Version = options.Version,
                Description = options.Description
            },
            Servers = servers,
            Channels = channels.Count > 0 ? channels : null,
            Operations = operationsV3,
            Components = new AsyncApiComponents
            {
                Messages = messages.Count > 0 ? messages : null,
                Schemas = schemas.Count > 0 ? schemas : null
            }
        };

        return options.Format == AsyncApiFormat.Json
            ? _jsonSerializer.Serialize(document)
            : _yamlSerializer.Serialize(document);
    }

    private static List<string> GetConsumerChannelKeys(ConsumerInfo consumer)
    {
        if (consumer.Topology is ConsumerAttributeTopology attrTopology && attrTopology.Bindings.Count > 0)
        {
            return attrTopology.Bindings
                .Select(b => string.IsNullOrEmpty(b.RoutingKey) ? b.ExchangeSource : $"{b.ExchangeSource}/{b.RoutingKey}")
                .Distinct()
                .ToList();
        }

        return [consumer.Topology.Queue];
    }

    private static Dictionary<string, AsyncApiServer> BuildServers(MessageBrokerSettings settings, bool isV3)
    {
        var servers = new Dictionary<string, AsyncApiServer>();

        if (settings.Brokers.Count > 0)
        {
            foreach (var (name, broker) in settings.Brokers)
            {
                var address = $"{broker.Host}:{broker.Port}";
                servers[name] = new AsyncApiServer
                {
                    Url = isV3 ? null : address,
                    Host = isV3 ? address : null,
                    Protocol = "amqp",
                    ProtocolVersion = "0.9.1",
                    Description = $"RabbitMQ broker '{name}'"
                };
            }
        }
        else
        {
            var referencedBrokers = settings.Producers.Select(p => p.Broker)
                .Concat(settings.Consumers.Select(c => c.Broker))
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct()
                .ToList();

            if (referencedBrokers.Count > 0)
            {
                foreach (var name in referencedBrokers)
                {
                    servers[name] = new AsyncApiServer
                    {
                        Url = isV3 ? null : "localhost:5672",
                        Host = isV3 ? "localhost:5672" : null,
                        Protocol = "amqp",
                        ProtocolVersion = "0.9.1",
                        Description = $"RabbitMQ broker '{name}'"
                    };
                }
            }
            else
            {
                servers["default-broker"] = new AsyncApiServer
                {
                    Url = isV3 ? null : "localhost:5672",
                    Host = isV3 ? "localhost:5672" : null,
                    Protocol = "amqp",
                    ProtocolVersion = "0.9.1",
                    Description = "Default RabbitMQ broker"
                };
            }
        }

        return servers;
    }

    private (Dictionary<string, AsyncApiMessage> Messages, Dictionary<string, AsyncApiSchema> Schemas) BuildComponents(
        IReadOnlyCollection<Type> messageTypes,
        IXmlDocumentationReader? xmlReader)
    {
        var messages = new Dictionary<string, AsyncApiMessage>();
        var schemas = new Dictionary<string, AsyncApiSchema>();

        foreach (var messageType in messageTypes.OrderBy(t => t.Name))
        {
            var summary = xmlReader?.GetTypeSummary(messageType);
            messages[messageType.Name] = new AsyncApiMessage
            {
                Name = messageType.Name,
                Title = messageType.Name,
                Summary = string.IsNullOrWhiteSpace(summary) ? null : summary,
                Payload = new AsyncApiSchemaRef
                {
                    Ref = $"#/components/schemas/{messageType.Name}"
                }
            };

            schemas[messageType.Name] = _jsonSchemaGenerator.Generate(messageType, xmlReader);
        }

        return (messages, schemas);
    }

    private static string EscapeJsonPointer(string token) =>
        token.Replace("~", "~0").Replace("/", "~1");

    private static IXmlDocumentationReader? ResolveXmlReader(
        IEnumerable<Assembly> assemblies,
        string? explicitXmlPath)
    {
        if (!string.IsNullOrEmpty(explicitXmlPath) && File.Exists(explicitXmlPath))
        {
            return XmlDocumentationReader.FromFile(explicitXmlPath);
        }

        return (from assembly in assemblies
            where !string.IsNullOrEmpty(assembly.Location)
            select Path.ChangeExtension(assembly.Location, ".xml")
            into xmlPath
            where File.Exists(xmlPath)
            select XmlDocumentationReader.FromFile(xmlPath)).FirstOrDefault();
    }
}
