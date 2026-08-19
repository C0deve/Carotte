using System.Reflection;
using System.Text;

namespace Carotte.Documentation;

public sealed class CarotteDocGenerator(
    IMermaidDiagramGenerator? mermaidGenerator = null,
    IProducerDocumenter? producerDocumenter = null,
    IConsumerDocumenter? consumerDocumenter = null,
    IDataContractDocumenter? dataContractDocumenter = null) : ICarotteDocGenerator
{
    private readonly IMermaidDiagramGenerator _mermaidGenerator = mermaidGenerator ?? new MermaidDiagramGenerator();
    private readonly IProducerDocumenter _producerDocumenter = producerDocumenter ?? new ProducerDocumenter();
    private readonly IConsumerDocumenter _consumerDocumenter = consumerDocumenter ?? new ConsumerDocumenter();
    private readonly IDataContractDocumenter _dataContractDocumenter = dataContractDocumenter ?? new DataContractDocumenter();

    public string Generate(Assembly assembly, CarotteDocumentationOptions? options = null) =>
        Generate([assembly], options);

    public string Generate(IReadOnlyCollection<Assembly> assemblies, CarotteDocumentationOptions? options = null)
    {
        options ??= new CarotteDocumentationOptions();
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

    public string Generate(MessageBrokerSettings settings, CarotteDocumentationOptions? options = null) =>
        GenerateInternal(settings, options ?? new CarotteDocumentationOptions(), null);

    public string Generate(CarotteBuilder builder, CarotteDocumentationOptions? options = null)
    {
        options ??= new CarotteDocumentationOptions();
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
        CarotteDocumentationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await GenerateToFileAsync([assembly], outputPath, options, cancellationToken);

    public async Task GenerateToFileAsync(
        IReadOnlyCollection<Assembly> assemblies,
        string outputPath,
        CarotteDocumentationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var markdown = Generate(assemblies, options);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
    }

    private string GenerateInternal(
        MessageBrokerSettings settings,
        CarotteDocumentationOptions options,
        IXmlDocumentationReader? xmlReader)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {options.Title}");
        sb.AppendLine();

        if (options.IncludeMermaidDiagram)
        {
            sb.Append(_mermaidGenerator.Generate(settings));
        }

        if (options.IncludeProducers)
        {
            sb.Append(_producerDocumenter.Generate(settings.Producers));
            sb.AppendLine();
        }

        if (options.IncludeConsumers)
        {
            sb.Append(_consumerDocumenter.Generate(settings.Consumers));
            sb.AppendLine();
        }

        if (options.IncludeDataContracts)
        {
            var messageTypes = settings.Producers
                .Select(p => p.MessageType)
                .Concat(settings.Consumers.SelectMany(c => c.MessageTypes))
                .Distinct()
                .ToList();

            sb.Append(_dataContractDocumenter.Generate(messageTypes, xmlReader));
        }

        return sb.ToString();
    }

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
