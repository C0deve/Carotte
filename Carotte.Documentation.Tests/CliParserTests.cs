using Carotte.DocCli;
using Shouldly;

namespace Carotte.Documentation.Tests;

public class CliParserTests
{
    [Fact]
    public void Parse_WhenAssemblyPassedWithLongOption_ShouldSetAssemblyPath()
    {
        // Arrange
        string[] args = ["--assembly", "MyService.dll"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.AssemblyPath.ShouldBe("MyService.dll");
    }

    [Fact]
    public void Parse_WhenAssemblyPassedWithShortOption_ShouldSetAssemblyPath()
    {
        // Arrange
        string[] args = ["-a", "MyService.dll"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.AssemblyPath.ShouldBe("MyService.dll");
    }

    [Fact]
    public void Parse_WhenOutputOptionPassed_ShouldSetOutputPath()
    {
        // Arrange
        string[] args = ["-a", "MyService.dll", "-o", "docs/MESSAGING.md"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.OutputPath.ShouldBe("docs/MESSAGING.md");
    }

    [Fact]
    public void Parse_WhenTitleOptionPassed_ShouldSetTitle()
    {
        // Arrange
        string[] args = ["-a", "MyService.dll", "--title", "Order Service Docs"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.Title.ShouldBe("Order Service Docs");
    }

    [Fact]
    public void Parse_WhenNoDiagramFlagPassed_ShouldDisableMermaidDiagram()
    {
        // Arrange
        string[] args = ["-a", "MyService.dll", "--no-diagram"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.IncludeDiagram.ShouldBeFalse();
    }

    [Fact]
    public void Parse_WhenHelpFlagPassed_ShouldSetShowHelp()
    {
        // Arrange
        string[] args = ["--help"];

        // Act
        var options = CliParser.Parse(args);

        // Assert
        options.ShowHelp.ShouldBeTrue();
    }
}
