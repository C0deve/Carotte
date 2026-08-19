using System.Reflection;

namespace Carotte.Documentation;

public interface ICarotteDocGenerator
{
    string Generate(Assembly assembly, CarotteDocumentationOptions? options = null);
    string Generate(IReadOnlyCollection<Assembly> assemblies, CarotteDocumentationOptions? options = null);
    string Generate(MessageBrokerSettings settings, CarotteDocumentationOptions? options = null);
    string Generate(CarotteBuilder builder, CarotteDocumentationOptions? options = null);
    Task GenerateToFileAsync(Assembly assembly, string outputPath, CarotteDocumentationOptions? options = null, CancellationToken cancellationToken = default);
    Task GenerateToFileAsync(IReadOnlyCollection<Assembly> assemblies, string outputPath, CarotteDocumentationOptions? options = null, CancellationToken cancellationToken = default);
}
