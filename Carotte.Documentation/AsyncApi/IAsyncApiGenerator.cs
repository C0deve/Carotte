using System.Reflection;

namespace Carotte.Documentation.AsyncApi;

public interface IAsyncApiGenerator
{
    string Generate(Assembly assembly, CarotteAsyncApiOptions? options = null);
    string Generate(IReadOnlyCollection<Assembly> assemblies, CarotteAsyncApiOptions? options = null);
    string Generate(MessageBrokerSettings settings, CarotteAsyncApiOptions? options = null);
    string Generate(CarotteBuilder builder, CarotteAsyncApiOptions? options = null);
    Task GenerateToFileAsync(Assembly assembly, string outputPath, CarotteAsyncApiOptions? options = null, CancellationToken cancellationToken = default);
    Task GenerateToFileAsync(IReadOnlyCollection<Assembly> assemblies, string outputPath, CarotteAsyncApiOptions? options = null, CancellationToken cancellationToken = default);
}
