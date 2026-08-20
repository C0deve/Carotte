using System.Reflection;
using System.Text.RegularExpressions;

namespace Carotte.Documentation.AsyncApi;

public sealed partial class AsyncApiGenerator(
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
        var servers = BuildServers(settings);
        var messageTypes = settings.Producers
            .Select(p => p.MessageType)
            .Concat(settings.Consumers.SelectMany(c => c.MessageTypes))
            .Distinct()
            .ToList();

        var (messages, schemas) = BuildComponents(messageTypes, xmlReader);

        var channels = new Dictionary<string, AsyncApiChannel>();
        var operations = new Dictionary<string, AsyncApiOperation>();

        foreach (var producer in settings.Producers)
        {
            var channelAddress = string.IsNullOrEmpty(producer.RoutingKey)
                ? producer.ExchangePublication
                : $"{producer.ExchangePublication}/{producer.RoutingKey}";

            var channelKey = SanitizeChannelKey(string.IsNullOrEmpty(producer.RoutingKey)
                ? producer.ExchangePublication
                : $"{producer.ExchangePublication}.{producer.RoutingKey}");

            if (!channels.TryGetValue(channelKey, out var channel))
            {
                channel = new AsyncApiChannel
                {
                    Address = channelAddress,
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
                                AutoDelete = producer.AutoDelete,
                                Vhost = "/"
                            },
                            BindingVersion = "0.3.0"
                        }
                    }
                };
                channels[channelKey] = channel;
            }

            channel.Messages ??= new Dictionary<string, AsyncApiMessageRef>();
            channel.Messages[producer.MessageType.Name] = new AsyncApiMessageRef
            {
                Ref = $"#/components/messages/{producer.MessageType.Name}"
            };

            var opId = $"publish{producer.MessageType.Name}";
            operations[opId] = new AsyncApiOperation
            {
                Action = "send",
                Summary = $"Publishes {producer.MessageType.Name} message",
                Channel = new AsyncApiChannelRef { Ref = $"#/channels/{channelKey}" },
                Messages = [new AsyncApiMessageRef { Ref = $"#/components/messages/{producer.MessageType.Name}" }]
            };
        }

        foreach (var consumer in settings.Consumers)
        {
            var channelAddresses = GetConsumerChannelAddresses(consumer);

            foreach (var channelAddress in channelAddresses)
            {
                var channelKey = SanitizeChannelKey(channelAddress);

                if (!channels.TryGetValue(channelKey, out var channel))
                {
                    channel = new AsyncApiChannel
                    {
                        Address = channelAddress,
                        Bindings = new AsyncApiChannelBindings
                        {
                            Amqp = new AsyncApiAmqpChannelBinding
                            {
                                Is = "queue",
                                Queue = new AsyncApiAmqpQueueBinding
                                {
                                    Name = string.IsNullOrWhiteSpace(consumer.Topology.Queue) ? "default-queue" : consumer.Topology.Queue,
                                    Durable = true,
                                    Exclusive = false,
                                    AutoDelete = false,
                                    Vhost = "/"
                                },
                                BindingVersion = "0.3.0"
                            }
                        }
                    };
                    channels[channelKey] = channel;
                }

                var primaryMessageType = consumer.MessageTypes.FirstOrDefault();
                var messageRef = primaryMessageType != null
                    ? new AsyncApiMessageRef { Ref = $"#/components/messages/{primaryMessageType.Name}" }
                    : null;

                if (primaryMessageType != null)
                {
                    channel.Messages ??= new Dictionary<string, AsyncApiMessageRef>();
                    channel.Messages[primaryMessageType.Name] = messageRef!;
                }

                var opId = $"consume{consumer.ConsumerType.Name}";
                operations[opId] = new AsyncApiOperation
                {
                    Action = "receive",
                    Summary = $"Consumes messages by {consumer.ConsumerType.Name}",
                    Channel = new AsyncApiChannelRef { Ref = $"#/channels/{channelKey}" },
                    Bindings = new AsyncApiOperationBindings
                    {
                        Amqp = new AsyncApiAmqpOperationBinding
                        {
                            Ack = true,
                            BindingVersion = "0.3.0"
                        }
                    },
                    Messages = messageRef != null ? [messageRef] : null
                };
            }
        }

        var document = new AsyncApiDocument
        {
            AsyncApi = "3.1.0",
            Info = new AsyncApiInfo
            {
                Title = options.Title,
                Version = options.Version,
                Description = options.Description
            },
            Servers = servers.Count > 0 ? servers : null,
            Channels = channels.Count > 0 ? channels : null,
            Operations = operations.Count > 0 ? operations : null,
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

    private static List<string> GetConsumerChannelAddresses(ConsumerInfo consumer)
    {
        if (consumer.Topology is ConsumerAttributeTopology attrTopology && attrTopology.Bindings.Count > 0)
        {
            var addresses = attrTopology.Bindings
                .Select(b =>
                {
                    if (!string.IsNullOrEmpty(b.RoutingKey) && !string.IsNullOrEmpty(b.ExchangeSource))
                    {
                        return $"{b.ExchangeSource}/{b.RoutingKey}";
                    }

                    if (!string.IsNullOrEmpty(b.ExchangeSource))
                    {
                        return b.ExchangeSource;
                    }

                    return consumer.Topology.Queue;
                })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            if (addresses.Count > 0)
            {
                return addresses;
            }
        }

        return [string.IsNullOrWhiteSpace(consumer.Topology.Queue) ? "default-queue" : consumer.Topology.Queue];
    }

    private static Dictionary<string, AsyncApiServer> BuildServers(MessageBrokerSettings settings)
    {
        var servers = new Dictionary<string, AsyncApiServer>();

        if (settings.Brokers.Count > 0)
        {
            foreach (var (name, broker) in settings.Brokers)
            {
                var serverKey = SanitizeChannelKey(name);
                servers[serverKey] = new AsyncApiServer
                {
                    Host = $"{broker.Host}:{broker.Port}",
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
                    var serverKey = SanitizeChannelKey(name);
                    servers[serverKey] = new AsyncApiServer
                    {
                        Host = "localhost:5672",
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
                    Host = "localhost:5672",
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

    private static string SanitizeChannelKey(string key)
    {
        var sanitized = key.Replace('/', '.').Replace('#', '_').Replace('*', '_');
        sanitized = ChannelKeyRegex().Replace(sanitized, "_");
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\.\-_]")]
    private static partial Regex ChannelKeyRegex();

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
