using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator.Tests.Infrastructure;

public abstract class GeneratorTestBase
{
    private static readonly string[] AbstractionsAssemblyPaths =
    [
        typeof(IRequestHandler<,>).Assembly.Location,
        typeof(object).Assembly.Location,
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll"),
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Threading.Tasks.dll")
    ];

    protected static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(
        string source,
        string assemblyName = "TestAssembly")
    {
        var references = AbstractionsAssemblyPaths
            .Where(File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DecoratRIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }

    protected static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunTwoStageGenerator(
        string handlerSource,
        string compositionRootSource,
        string handlerAssemblyName = "HandlerLib",
        string compositionRootAssemblyName = "CompositionRoot")
    {
        var references = AbstractionsAssemblyPaths
            .Where(File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToArray();

        var handlerCompilation = CSharpCompilation.Create(
            handlerAssemblyName,
            [CSharpSyntaxTree.ParseText(handlerSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator1 = new DecoratRIncrementalGenerator();
        GeneratorDriver handlerDriver = CSharpGeneratorDriver.Create(generator1);

        handlerDriver = handlerDriver.RunGeneratorsAndUpdateCompilation(
            handlerCompilation, out var handlerOutputCompilation, out _);

        var handlerRef = handlerOutputCompilation.ToMetadataReference();

        var compositionRootReferences = references.ToList();
        compositionRootReferences.Add(handlerRef);

        var compositionRootCompilation = CSharpCompilation.Create(
            compositionRootAssemblyName,
            [CSharpSyntaxTree.ParseText(compositionRootSource)],
            compositionRootReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator2 = new DecoratRIncrementalGenerator();
        GeneratorDriver compositionRootDriver = CSharpGeneratorDriver.Create(generator2);

        compositionRootDriver = compositionRootDriver.RunGeneratorsAndUpdateCompilation(
            compositionRootCompilation, out _, out var diagnostics);

        var runResult = compositionRootDriver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }
}
