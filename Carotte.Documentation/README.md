# Carotte.Documentation 🥕📖

[![NuGet Version](https://img.shields.io/nuget/v/Carotte.Documentation.svg)](https://www.nuget.org/packages/Carotte.Documentation)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

**Carotte.Documentation** is an automated documentation generation engine for **Carotte** microservices. It inspects compiled assemblies and C# XML doc comments to generate comprehensive **Markdown specifications**, interactive **Mermaid topology diagrams**, and **AsyncAPI v3.0** definitions (YAML / JSON) with JSON Schemas.

---

## 🚀 Features

- 📄 **AsyncAPI v3.0 Specification Export**: Full AsyncAPI 3.0 export (YAML / JSON) complete with AMQP channel bindings (exchanges, queues, routing keys, dead letters).
- 📊 **Interactive Mermaid Diagrams**: Visual representation (`graph LR`) of message publishers, exchanges, queues, consumers, and Dead-Letter Exchanges/Queues (DLX/DLQ).
- 📋 **Data Contracts & JSON Schema**: Automatic extraction of payload schemas, property types, and XML comments (`/// <summary>`).
- 🛡️ **Specification Validation**: Built-in `AsyncApiDocumentValidator` to ensure generated AsyncAPI documents strictly conform to the AsyncAPI 3.0 standard.
- 🧪 **Architecture & Unit Testing**: Execute documentation generation as part of your test suite (xUnit, NUnit) to enforce documentation up-to-date validation and prevent documentation drift (*doc drift*) in CI/CD pipelines.

---

## 📦 Installation

Install the package via NuGet:

```bash
dotnet add package Carotte.Documentation
```

---

## 💻 Programmatic Usage

### 1. Generate Markdown Documentation

```csharp
using Carotte.Documentation;

var generator = new CarotteDocGenerator();

// Generate Markdown string
string markdown = generator.Generate(typeof(Program).Assembly);

// Or generate directly to a file
await generator.GenerateToFileAsync(
    typeof(Program).Assembly, 
    outputPath: "docs/MESSAGING.md",
    options: new CarotteDocumentationOptions
    {
        Title = "Order Service Messaging Architecture",
        IncludeDiagram = true,
        IncludeDataContracts = true
    });
```

---

### 2. Generate AsyncAPI v3.0 (YAML / JSON)

```csharp
using Carotte.Documentation.AsyncApi;

var asyncApiGenerator = new AsyncApiGenerator();

var options = new CarotteAsyncApiOptions
{
    Title = "Order Microservice Messaging API",
    Version = "1.0.0",
    Validate = true // Validates AsyncAPI schema automatically
};

// Generate YAML
string yaml = await asyncApiGenerator.GenerateYamlAsync(typeof(Program).Assembly, options);
await File.WriteAllTextAsync("docs/asyncapi.yaml", yaml);

// Generate JSON
string json = await asyncApiGenerator.GenerateJsonAsync(typeof(Program).Assembly, options);
await File.WriteAllTextAsync("docs/asyncapi.json", json);
```

---

### 3. Generate Standalone Mermaid Diagram

```csharp
using Carotte.Documentation;

var diagramGenerator = new MermaidDiagramGenerator();
string mermaidGraph = diagramGenerator.GenerateDiagram(typeof(Program).Assembly);
```

---

## 🧪 Architecture Testing Example (CI Guard against Doc Drift)

Use `Carotte.Documentation` in an automated test to ensure documentation stays in sync with code changes:

```csharp
[Fact]
public async Task MessagingDocumentation_ShouldBeUpToDate()
{
    var generator = new CarotteDocGenerator();
    var currentDoc = generator.Generate(typeof(Program).Assembly);

    var existingDocPath = Path.Combine(AppContext.BaseDirectory, "../../../docs/MESSAGING.md");
    var existingDoc = await File.ReadAllTextAsync(existingDocPath);

    Assert.Equal(existingDoc.Trim(), currentDoc.Trim());
}
```

---

## ⚙️ Configuration Options

### `CarotteDocumentationOptions`

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Title` | `string?` | Assembly name | Document main heading |
| `IncludeDiagram` | `bool` | `true` | Include Mermaid topology graph |
| `IncludeDataContracts` | `bool` | `true` | Include detailed message property schemas |
| `XmlDocPath` | `string?` | `null` | Path to compiler-generated XML doc comments |
| `Namespaces` | `IReadOnlyList<string>` | `[]` | Filter scanned types by namespaces |

---

## 📄 License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).
