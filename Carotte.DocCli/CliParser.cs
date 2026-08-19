namespace Carotte.DocCli;

public static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        string? assemblyPath = null;
        string? outputPath = null;
        string? title = null;
        string? xmlDocPath = null;
        var namespaces = new List<string>();
        var includeDiagram = true;
        var includeContracts = true;
        var format = "markdown";
        string? apiVersion = null;
        string? specVersion = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h" or "--help":
                    showHelp = true;
                    break;
                case "-a" or "--assembly" when i + 1 < args.Length:
                    assemblyPath = args[++i];
                    break;
                case "-o" or "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "-t" or "--title" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "-x" or "--xml-doc" when i + 1 < args.Length:
                    xmlDocPath = args[++i];
                    break;
                case "-f" or "--format" when i + 1 < args.Length:
                    format = args[++i];
                    break;
                case "--api-version" when i + 1 < args.Length:
                    apiVersion = args[++i];
                    break;
                case "--spec-version" when i + 1 < args.Length:
                    specVersion = args[++i];
                    break;
                case "-n" or "--namespaces" when i + 1 < args.Length:
                {
                    var nsList = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    namespaces.AddRange(nsList);
                    break;
                }
                case "--no-diagram":
                    includeDiagram = false;
                    break;
                case "--no-contracts":
                    includeContracts = false;
                    break;
            }
        }

        return new CliOptions
        {
            AssemblyPath = assemblyPath,
            OutputPath = outputPath,
            Title = title,
            XmlDocPath = xmlDocPath,
            Namespaces = namespaces.AsReadOnly(),
            IncludeDiagram = includeDiagram,
            IncludeContracts = includeContracts,
            Format = format,
            ApiVersion = apiVersion,
            SpecVersion = specVersion,
            ShowHelp = showHelp
        };
    }
}
