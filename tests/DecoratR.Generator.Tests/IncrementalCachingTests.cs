using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DecoratR.Generator.Tests;

public class IncrementalCachingTests : GeneratorTestBase
{
    private const string Unrelated = "public class Unrelated { public int X; }";
    private const string UnrelatedEdited = "public class Unrelated { public int X; public int Y; }";

    [Fact]
    public void UnrelatedEdit_KeepsAllOutputsCached()
    {
        var (driver, compilation, unrelatedTree) = RunOnce(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1)));

        var edited = compilation.ReplaceSyntaxTree(unrelatedTree, CSharpSyntaxTree.ParseText(UnrelatedEdited, path: "Unrelated.cs", cancellationToken: Ct));
        var result = driver.RunGenerators(edited, Ct).GetRunResult().Results[0];

        foreach (var (name, steps) in result.TrackedOutputSteps)
            steps.SelectMany(s => s.Outputs).Select(o => o.Reason).Should().OnlyContain(
                r => r == IncrementalStepRunReason.Cached || r == IncrementalStepRunReason.Unchanged,
                $"output step '{name}' must be cached after an unrelated edit");

        result.TrackedSteps[TrackingNames.RegistrationsInput].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void UnrelatedEdit_InLibrary_DoesNotRescanReferences()
    {
        var (driver, compilation, unrelatedTree) = RunOnce(TestSources.HandlerOnly());

        var edited = compilation.ReplaceSyntaxTree(unrelatedTree, CSharpSyntaxTree.ParseText(UnrelatedEdited, path: "Unrelated.cs", cancellationToken: Ct));
        var result = driver.RunGenerators(edited, Ct).GetRunResult().Results[0];

        // In a metadata-only library the referenced-assembly scan short-circuits, so its output is unchanged.
        result.TrackedSteps[TrackingNames.Referenced].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
        result.TrackedSteps[TrackingNames.MetadataInput].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void HandlerEdit_RegeneratesRegistrations()
    {
        var source = TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1));
        var (driver, compilation, _) = RunOnce(source);

        var sourceTree = compilation.SyntaxTrees.Single(t => t.FilePath == "Source.cs");
        var editedSource = source.Replace("TestCommandHandler", "RenamedHandler");
        var edited = compilation.ReplaceSyntaxTree(sourceTree, CSharpSyntaxTree.ParseText(editedSource, path: "Source.cs", cancellationToken: Ct));

        var result = driver.RunGenerators(edited, Ct).GetRunResult().Results[0];

        result.TrackedSteps[TrackingNames.Handlers].Single().Outputs.Single().Reason.Should().Be(IncrementalStepRunReason.Modified);
        result.TrackedSteps[TrackingNames.RegistrationsInput].Single().Outputs.Single().Reason.Should().Be(IncrementalStepRunReason.Modified);
        result.GeneratedSources.Single(s => s.HintName == GenerationResult.RegistrationsHint).SourceText.ToString()
            .Should().Contain("RenamedHandler").And.NotContain("TestCommandHandler");
    }

    [Fact]
    public void MetadataModels_HaveStructuralEquality()
    {
        var (driver, compilation, unrelatedTree) = RunOnce(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1)));

        // Re-parsing the same source produces new symbols; the extracted models must still compare equal.
        var sourceTree = compilation.SyntaxTrees.Single(t => t.FilePath == "Source.cs");
        var reparsed = compilation
            .ReplaceSyntaxTree(sourceTree, CSharpSyntaxTree.ParseText(sourceTree.ToString(), path: "Source.cs", cancellationToken: Ct))
            .ReplaceSyntaxTree(unrelatedTree, CSharpSyntaxTree.ParseText(Unrelated, path: "Unrelated.cs", cancellationToken: Ct));

        var result = driver.RunGenerators(reparsed, Ct).GetRunResult().Results[0];

        result.TrackedSteps[TrackingNames.Handlers].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
        result.TrackedSteps[TrackingNames.Decorators].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
        result.TrackedSteps[TrackingNames.RegistrationsInput].Single().Outputs.Single().Reason
            .Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
    }

    private static (GeneratorDriver Driver, CSharpCompilation Compilation, SyntaxTree UnrelatedTree) RunOnce(string source)
    {
        var unrelatedTree = CSharpSyntaxTree.ParseText(Unrelated, path: "Unrelated.cs", cancellationToken: Ct);
        var compilation = CreateCompilation(source, "TestAssembly").AddSyntaxTrees(unrelatedTree);

        var driver = CreateDriver(trackIncrementalSteps: true).RunGenerators(compilation, Ct);
        return (driver, compilation, unrelatedTree);
    }
}
