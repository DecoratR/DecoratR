# Project Guidelines

## Architecture

See [architecture.md](../architecture.md) for the generator pipeline, project map, cross-assembly composition model, diagnostics and key design decisions.

## Build and Test

```bash
dotnet build                     # Build solution
dotnet test                      # Run all tests (generator tests + integration tests)
dotnet build -p:CI=true          # CI mode: warnings as errors
UPDATE_SNAPSHOTS=1 dotnet test   # Re-write tests/DecoratR.Generator.Tests/Snapshots/*.verified.cs after an intended output change
```

- .NET SDK version is pinned in `global.json`. The generator references the Roslyn version of the .NET 10 SDK baseline; do not bump `Microsoft.CodeAnalysis.CSharp` in `src/DecoratR.Generator` casually (see the comment in the csproj).
- `EmitCompilerGeneratedFiles=true` is set in `Directory.Build.props` — inspect generated output in `obj/`.
- New diagnostics go into `Diagnostics.cs` **and** `AnalyzerReleases.Unshipped.md` (release tracking is enforced).

## Code Style

- **Naming:** PascalCase for types/methods. Diagnostic IDs use `DCTR###`. Generated registries are `{Namespace}.DecoratRHandlerRegistry` / `DecoratRDecoratorRegistry`; `AddDecoratR()` lives in `Microsoft.Extensions.DependencyInjection`.
- **Pipeline values are records** (`Model/`) with `EquatableArray<T>` for collections and `LocationInfo` instead of `Location`, so everything stays cacheable. Never put symbols or syntax nodes into pipeline values.
- **Emitters use `SourceWriter`** (`Emit/SourceWriter.cs`): `Block()` scopes for braces, `Indent()` scopes, `AppendQuoted` for string literals, `AppendTypeForDoc` for XML docs. No literal indentation strings.
- **Fully qualified type names** with `global::` prefix in all generated code; names of well-known types live in `WellKnownTypes`.
- **Generator source is netstandard2.0**; records work through `Polyfills/IsExternalInit.cs`.

## Testing

- **Framework:** xunit.v3 with AwesomeAssertions.
- **Test base:** `GeneratorTestBase` provides `RunGenerator()` (single assembly) and `RunTwoStageGenerator()` (library + composition root). Both return a `GenerationResult` with generator diagnostics, generated sources by hint name and the output compilation.
- **Always call `.ShouldCompile()`** on results whose generated code matters — the whole point of the suite is that generated code compiles.
- **Helpers:** `GetPipeline(request, response)`, `GetPipelineSteps()`, `GetApplyMethod(name)` in `GeneratedOutputExtensions`; `Snapshot.Verify(name, text)` for full-output snapshots.
- **Fixtures:** `TestSources` has reusable source snippets and builders (`Decorator()`, `HandlerOnly()`, `FullPath()`, `EmptyHost()`, stream variants).
- **Integration tests** (`tests/DecoratR.IntegrationTests`) use the generator as an analyzer and run the real DI pipeline; add a scenario there when runtime behaviour changes.
