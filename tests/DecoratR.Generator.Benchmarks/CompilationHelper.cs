using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator.Benchmarks;

/// <summary>
/// Shared compilation helpers for benchmarks. Mirrors the test infrastructure
/// in GeneratorTestBase but without xunit dependencies.
/// </summary>
internal static class CompilationHelper
{
    private static readonly MetadataReference[] References = BuildReferences();

    private static MetadataReference[] BuildReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var paths = new[]
        {
            typeof(IRequestHandler<,>).Assembly.Location,
            typeof(object).Assembly.Location,
            Path.Combine(runtimeDir, "System.Runtime.dll"),
            Path.Combine(runtimeDir, "System.Threading.Tasks.dll")
        };

        return paths
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "BenchmarkAssembly")
    {
        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriver CreateDriver()
    {
        return CSharpGeneratorDriver.Create(new DecoratRIncrementalGenerator());
    }

    /// <summary>
    /// Creates a two-stage setup: compiles the handler assembly with the generator,
    /// then returns a compilation for the composition root that references the handler output.
    /// </summary>
    public static (CSharpCompilation Compilation, GeneratorDriver Driver) CreateTwoStageSetup(
        string handlerSource,
        string compositionRootSource,
        string handlerAssemblyName = "HandlerLib",
        string compositionRootAssemblyName = "CompositionRoot")
    {
        var handlerCompilation = CreateCompilation(handlerSource, handlerAssemblyName);
        GeneratorDriver handlerDriver = CSharpGeneratorDriver.Create(new DecoratRIncrementalGenerator());

        handlerDriver.RunGeneratorsAndUpdateCompilation(
            handlerCompilation, out var handlerOutputCompilation, out _);

        var handlerRef = handlerOutputCompilation.ToMetadataReference();

        var compositionRootCompilation = CSharpCompilation.Create(
            compositionRootAssemblyName,
            [CSharpSyntaxTree.ParseText(compositionRootSource)],
            References.Append(handlerRef).ToArray(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compositionDriver = CSharpGeneratorDriver.Create(new DecoratRIncrementalGenerator());

        return (compositionRootCompilation, compositionDriver);
    }
}
