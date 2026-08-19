using System.Reflection;

namespace Carotte.Documentation;

public static class CarotteDocumentationExtensions
{
    public static string GenerateDocumentation(this CarotteBuilder builder, CarotteDocumentationOptions? options = null) =>
        new CarotteDocGenerator().Generate(builder, options);

    public static string GenerateDocumentation(this Assembly assembly, CarotteDocumentationOptions? options = null) =>
        new CarotteDocGenerator().Generate(assembly, options);

    public static string GenerateDocumentation(this IReadOnlyCollection<Assembly> assemblies, CarotteDocumentationOptions? options = null) =>
        new CarotteDocGenerator().Generate(assemblies, options);

    public static async Task GenerateDocumentationFileAsync(
        this Assembly assembly,
        string outputPath,
        CarotteDocumentationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await new CarotteDocGenerator().GenerateToFileAsync(assembly, outputPath, options, cancellationToken);
}
