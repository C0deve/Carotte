using System.Runtime.Loader;
using Carotte.Documentation;
using Carotte.Documentation.AsyncApi;

namespace Carotte.DocCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CliParser.Parse(args);

        if (options.ShowHelp || string.IsNullOrEmpty(options.AssemblyPath))
        {
            PrintUsage();
            return options.ShowHelp ? 0 : 1;
        }

        var fullAssemblyPath = Path.GetFullPath(options.AssemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Error: Assembly file not found at '{fullAssemblyPath}'.");
            return 1;
        }

        try
        {
            var assemblyLoadContext = new AssemblyLoadContext("CarotteDocContext", isCollectible: true);
            var assembly = assemblyLoadContext.LoadFromAssemblyPath(fullAssemblyPath);

            var isAsyncApi = options.Format.StartsWith("asyncapi", StringComparison.OrdinalIgnoreCase) ||
                             options.Format.Equals("yaml", StringComparison.OrdinalIgnoreCase) ||
                             options.Format.Equals("json", StringComparison.OrdinalIgnoreCase);

            string outputContent;

            if (isAsyncApi)
            {
                var asyncApiFormat = options.Format.EndsWith("json", StringComparison.OrdinalIgnoreCase)
                    ? AsyncApiFormat.Json
                    : AsyncApiFormat.Yaml;

                var asyncApiOptions = new CarotteAsyncApiOptions
                {
                    Title = options.Title ?? $"{assembly.GetName().Name} Messaging API",
                    Version = options.ApiVersion ?? "1.0.0",
                    Format = asyncApiFormat,
                    XmlDocumentationPath = options.XmlDocPath,
                    Namespaces = options.Namespaces
                };

                var generator = new AsyncApiGenerator();
                outputContent = generator.Generate(assembly, asyncApiOptions);

                if (options.Validate)
                {
                    var validator = new AsyncApiDocumentValidator();
                    var validationResult = validator.Validate(outputContent);
                    if (!validationResult.IsValid)
                    {
                        await Console.Error.WriteLineAsync("AsyncAPI Document Validation Errors:");
                        foreach (var error in validationResult.Errors)
                        {
                            await Console.Error.WriteLineAsync($" - {error}");
                        }
                        return 1;
                    }

                    await Console.Out.WriteLineAsync("AsyncAPI Document is valid.");
                }
            }
            else
            {
                var docOptions = new CarotteDocumentationOptions
                {
                    Title = options.Title ?? $"{assembly.GetName().Name} Messaging Specification",
                    IncludeMermaidDiagram = options.IncludeDiagram,
                    IncludeDataContracts = options.IncludeContracts,
                    XmlDocumentationPath = options.XmlDocPath,
                    Namespaces = options.Namespaces
                };

                var generator = new CarotteDocGenerator();
                outputContent = generator.Generate(assembly, docOptions);
            }

            if (!string.IsNullOrEmpty(options.OutputPath))
            {
                var fullOutputPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullOutputPath, outputContent);
                await Console.Out.WriteLineAsync($"Successfully generated documentation at: {fullOutputPath}");
            }
            else
            {
                await Console.Out.WriteLineAsync(outputContent);
            }

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error generating documentation: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Carotte Documentation Generator CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/Carotte.DocCli -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -a, --assembly <path>       Path to the compiled assembly (.dll) [Required]");
        Console.WriteLine("  -o, --output <path>         Output path for the generated file (defaults to stdout)");
        Console.WriteLine("  -f, --format <format>       Output format: markdown, asyncapi-yaml, asyncapi-json (defaults to markdown)");
        Console.WriteLine("  -t, --title <title>         Custom title for the document");
        Console.WriteLine("  --api-version <version>     API version in AsyncAPI document (defaults to 1.0.0)");
        Console.WriteLine("  -v, --validate              Validate generated AsyncAPI document against AsyncAPI 3.1 specification");
        Console.WriteLine("  -x, --xml-doc <path>        Path to XML documentation file (defaults to matching .xml alongside .dll)");
        Console.WriteLine("  -n, --namespaces <list>     Comma-separated list of namespaces to include in scan");
        Console.WriteLine("  --no-diagram                Disable Mermaid diagram generation (Markdown only)");
        Console.WriteLine("  --no-contracts              Disable data contracts schemas (Markdown only)");
        Console.WriteLine("  -h, --help                  Show this help message");
    }
}
