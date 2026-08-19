using System.Runtime.Loader;
using Carotte.Documentation;

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

            var docOptions = new CarotteDocumentationOptions
            {
                Title = options.Title ?? $"{assembly.GetName().Name} Messaging Specification",
                IncludeMermaidDiagram = options.IncludeDiagram,
                IncludeDataContracts = options.IncludeContracts,
                XmlDocumentationPath = options.XmlDocPath,
                Namespaces = options.Namespaces
            };

            var generator = new CarotteDocGenerator();
            var markdown = generator.Generate(assembly, docOptions);

            if (!string.IsNullOrEmpty(options.OutputPath))
            {
                var fullOutputPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullOutputPath, markdown);
                await Console.Out.WriteLineAsync($"Successfully generated documentation at: {fullOutputPath}");
            }
            else
            {
                await Console.Out.WriteLineAsync(markdown);
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
        Console.WriteLine("Carotte Markdown Documentation Generator CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/Carotte.DocCli -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -a, --assembly <path>     Path to the compiled assembly (.dll) [Required]");
        Console.WriteLine("  -o, --output <path>       Output path for the generated Markdown file (defaults to stdout)");
        Console.WriteLine("  -t, --title <title>       Custom title for the Markdown document");
        Console.WriteLine("  -x, --xml-doc <path>      Path to XML documentation file (defaults to matching .xml alongside .dll)");
        Console.WriteLine("  -n, --namespaces <list>   Comma-separated list of namespaces to include in scan");
        Console.WriteLine("  --no-diagram              Disable Mermaid diagram generation");
        Console.WriteLine("  --no-contracts            Disable data contracts schemas");
        Console.WriteLine("  -h, --help                Show this help message");
    }
}
