using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Tests.Infrastructure;

/// <summary>
/// The outcome of one generator run: generator diagnostics, generated sources by hint name and the compilation
/// that includes the generated code (used to prove that the generated code compiles).
/// </summary>
public sealed record GenerationResult(
    ImmutableArray<Diagnostic> Diagnostics,
    IReadOnlyDictionary<string, string> Sources,
    Compilation OutputCompilation,
    GeneratorDriverRunResult RunResult)
{
    public const string RegistrationsHint = "DecoratRServiceCollectionExtensions.g.cs";
    public const string HandlerRegistryHint = "DecoratRHandlerRegistry.g.cs";
    public const string DecoratorRegistryHint = "DecoratRDecoratorRegistry.g.cs";

    public string Registrations => Source(RegistrationsHint);

    public string HandlerRegistry => Source(HandlerRegistryHint);

    public string DecoratorRegistry => Source(DecoratorRegistryHint);

    public bool Has(string hintName) => Sources.ContainsKey(hintName);

    public string Source(string hintName)
    {
        if (Sources.TryGetValue(hintName, out var source)) return source;

        throw new InvalidOperationException(
            $"No generated source with hint name '{hintName}'. Generated: {string.Join(", ", Sources.Keys)}");
    }

    public IEnumerable<Diagnostic> DiagnosticsWithId(string id) => Diagnostics.Where(d => d.Id == id);

    public Diagnostic Diagnostic(string id) => Diagnostics.Single(d => d.Id == id);

    /// <summary>The source text a diagnostic points at (diagnostic locations are file/span based).</summary>
    public string LocationText(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var tree = OutputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == lineSpan.Path)
            ?? throw new InvalidOperationException($"Diagnostic {diagnostic.Id} has no file location (path '{lineSpan.Path}').");

        return tree.GetText().ToString(diagnostic.Location.SourceSpan);
    }

    public ImmutableArray<Diagnostic> CompileErrors =>
        OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

    /// <summary>Asserts that the compilation including the generated code has no errors.</summary>
    public GenerationResult ShouldCompile()
    {
        var errors = CompileErrors;
        if (errors.Length == 0) return this;

        var details = string.Join(Environment.NewLine, errors.Select(e =>
            $"  {e.Id} {e.GetMessage()} @ {e.Location.SourceTree?.FilePath}:{e.Location.GetLineSpan().StartLinePosition.Line + 1}"));

        var generated = string.Join(Environment.NewLine + Environment.NewLine, Sources.Select(s =>
            $"----- {s.Key} -----{Environment.NewLine}{WithLineNumbers(s.Value)}"));

        throw new InvalidOperationException(
            $"Generated code does not compile:{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}{generated}");
    }

    private static string WithLineNumbers(string text)
    {
        var lines = text.Split('\n');
        return string.Join(Environment.NewLine, lines.Select((l, i) => $"{i + 1,4}: {l.TrimEnd('\r')}"));
    }
}
