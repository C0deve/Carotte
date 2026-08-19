namespace Carotte.Documentation;

public interface IMermaidDiagramGenerator
{
    string Generate(MessageBrokerSettings settings);
}
