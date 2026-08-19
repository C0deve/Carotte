using System.Reflection;
using Carotte.Documentation.AsyncApi;

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

    public static string GenerateAsyncApi(this CarotteBuilder builder, CarotteAsyncApiOptions? options = null) =>
        new AsyncApiGenerator().Generate(builder, options);

    public static string GenerateAsyncApi(this Assembly assembly, CarotteAsyncApiOptions? options = null) =>
        new AsyncApiGenerator().Generate(assembly, options);

    public static string GenerateAsyncApi(this IReadOnlyCollection<Assembly> assemblies, CarotteAsyncApiOptions? options = null) =>
        new AsyncApiGenerator().Generate(assemblies, options);

    public static async Task GenerateAsyncApiFileAsync(
        this Assembly assembly,
        string outputPath,
        CarotteAsyncApiOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await new AsyncApiGenerator().GenerateToFileAsync(assembly, outputPath, options, cancellationToken);
}
