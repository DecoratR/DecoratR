using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Generator.Benchmarks;

/// <summary>
/// Shared compilation helpers for benchmarks. Mirrors the test infrastructure
/// in GeneratorTestBase but without xunit dependencies.
/// </summary>
internal static class CompilationHelper
{
    /// <summary>Hint names of the generated files, used to verify that a scenario produces the expected output.</summary>
    public const string HandlerRegistryHint = "DecoratRHandlerRegistry.g.cs";
    public const string DecoratorRegistryHint = "DecoratRDecoratorRegistry.g.cs";
    public const string RegistrationsHint = "DecoratRServiceCollectionExtensions.g.cs";

    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>
    /// The framework facades plus DecoratR.Abstractions and the DI abstractions, like the generator tests. Without the
    /// DI abstractions the generator reports DCTR009 and emits neither registrations nor decorator registries, so the
    /// benchmarks would only measure detection.
    /// </summary>
    private static ImmutableArray<MetadataReference> BuildReferences()
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
            .Append(MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location))
            .ToImmutableArray();
    }

    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "BenchmarkAssembly") =>
        CreateCompilation([(assemblyName + ".cs", source)], assemblyName, []);

    /// <summary>Creates a compilation with one syntax tree per source plus the global usings.</summary>
    public static CSharpCompilation CreateCompilation(
        IEnumerable<(string Path, string Source)> sources,
        string assemblyName,
        IEnumerable<MetadataReference> additionalReferences)
    {
        var trees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s.Source, ParseOptions, s.Path))
            .Append(CSharpSyntaxTree.ParseText(GlobalUsings, ParseOptions, "GlobalUsings.cs"));

        return CSharpCompilation.Create(
            assemblyName,
            trees,
            References.AddRange(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Replaces the syntax tree at <paramref name="path"/> with its text plus a trailing comment (a keystroke).</summary>
    public static CSharpCompilation WithEdit(CSharpCompilation compilation, string path)
    {
        var tree = compilation.SyntaxTrees.Single(t => t.FilePath == path);
        var edited = CSharpSyntaxTree.ParseText(tree.GetText().ToString() + Environment.NewLine + "// edit", ParseOptions, path);
        return compilation.ReplaceSyntaxTree(tree, edited);
    }

    public static GeneratorDriver CreateDriver()
    {
        return CSharpGeneratorDriver.Create(new DecoratRIncrementalGenerator());
    }

    /// <summary>
    /// Runs the generator once and throws when an expected file is missing or the output does not compile, so a
    /// broken setup fails the benchmark run instead of silently measuring an early exit (e.g. DCTR009).
    /// </summary>
    public static Compilation EnsureGenerates(Compilation compilation, params string[] expectedHintNames)
    {
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToHashSet(StringComparer.Ordinal);
        var missing = expectedHintNames.Where(hint => !generated.Contains(hint)).ToList();
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var generatorDiagnostics = driver.GetRunResult().Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();

        if (missing.Count > 0 || errors.Count > 0 || generatorDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                $"Benchmark setup for '{compilation.AssemblyName}' is invalid. " +
                $"Missing: [{string.Join(", ", missing)}]. " +
                $"Generator diagnostics: [{string.Join("; ", generatorDiagnostics.Select(d => d.Id + " " + d.GetMessage()))}]. " +
                $"Compile errors: [{string.Join("; ", errors.Take(5).Select(d => d.Id + " " + d.GetMessage()))}].");
        }

        return output;
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
        var handlerOutputCompilation = EnsureGenerates(
            CreateCompilation(handlerSource, handlerAssemblyName), HandlerRegistryHint, DecoratorRegistryHint);

        var compositionRootCompilation = CreateCompilation(
            [(compositionRootAssemblyName + ".cs", compositionRootSource)],
            compositionRootAssemblyName,
            [handlerOutputCompilation.ToMetadataReference()]);

        return (compositionRootCompilation, CreateDriver());
    }
}
