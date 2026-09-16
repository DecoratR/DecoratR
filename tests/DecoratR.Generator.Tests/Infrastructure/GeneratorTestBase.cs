using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DecoratR.Generator.Tests.Infrastructure;

public abstract class GeneratorTestBase
{
    /// <summary>The cancellation token of the running test.</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Implicit usings for test sources, so snippets only need <c>using DecoratR;</c>.
    /// </summary>
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    private static readonly Lazy<ImmutableArray<MetadataReference>> FrameworkReferences = new(BuildFrameworkReferences);

    private static readonly Lazy<ImmutableArray<MetadataReference>> DecoratRReferences = new(() =>
    [
        MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
    ]);

    /// <summary>Runs the generator on a single compilation with the default references.</summary>
    protected static GenerationResult RunGenerator(string source, string assemblyName = "TestAssembly") =>
        RunGenerator(CreateCompilation(source, assemblyName));

    /// <summary>Runs the generator on a compilation that does not reference DecoratR or the DI abstractions.</summary>
    protected static GenerationResult RunGeneratorWithoutDependencyInjection(string source, string assemblyName = "TestAssembly")
    {
        var references = FrameworkReferences.Value.Add(MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location));
        return RunGenerator(CreateCompilation(source, assemblyName, references));
    }

    protected static GenerationResult RunGenerator(CSharpCompilation compilation)
    {
        var driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics, Ct);
        return ToResult(driver, outputCompilation, diagnostics);
    }

    /// <summary>
    /// Compiles a library with <c>[GenerateDecoratRMetadata]</c> and a composition root that references it.
    /// </summary>
    protected static (GenerationResult Library, GenerationResult Host) RunTwoStageGenerator(
        string librarySource,
        string hostSource,
        string libraryAssemblyName = "HandlerLib",
        string hostAssemblyName = "CompositionRoot")
    {
        var library = RunGenerator(CreateCompilation(librarySource, libraryAssemblyName));
        library.ShouldCompile();

        var libraryReference = library.OutputCompilation.ToMetadataReference();
        var host = RunGenerator(CreateCompilation(hostSource, hostAssemblyName, DefaultReferences().Add(libraryReference)));

        return (library, host);
    }

    protected static CSharpCompilation CreateCompilation(string source, string assemblyName) =>
        CreateCompilation(source, assemblyName, DefaultReferences());

    protected static CSharpCompilation CreateCompilation(string source, string assemblyName, ImmutableArray<MetadataReference> references) =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, path: "Source.cs"), CSharpSyntaxTree.ParseText(GlobalUsings, path: "GlobalUsings.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    protected static GeneratorDriver CreateDriver(bool trackIncrementalSteps = false) =>
        CSharpGeneratorDriver.Create(
            [new DecoratRIncrementalGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalSteps));

    protected static ImmutableArray<MetadataReference> DefaultReferences() =>
        FrameworkReferences.Value.AddRange(DecoratRReferences.Value);

    protected static GenerationResult ToResult(GeneratorDriver driver, Compilation outputCompilation, ImmutableArray<Diagnostic> diagnostics)
    {
        var runResult = driver.GetRunResult();
        var sources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .ToDictionary(s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal);

        return new GenerationResult(diagnostics, sources, outputCompilation, runResult);
    }

    private static ImmutableArray<MetadataReference> BuildFrameworkReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return trustedAssemblies
            .Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name.StartsWith("System.", StringComparison.Ordinal) || name is "System" or "netstandard" or "mscorlib";
            })
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
