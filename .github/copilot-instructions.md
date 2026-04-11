# Project Guidelines

## Architecture

See [architecture.md](../architecture.md) for the full generator pipeline, project map, cross-assembly composition model, and key design decisions.

## Build and Test

```bash
dotnet build              # Build solution
dotnet test               # Run all tests
dotnet test -c Release    # CI mode (warnings as errors when CI=true)
```

- .NET SDK version is pinned in `global.json` (currently 10.0.0).
- `EmitCompilerGeneratedFiles=true` is set in `Directory.Build.props` — inspect generated output in `obj/`.

## Code Style

- **Naming:** PascalCase for types/methods. Diagnostic IDs use `DCTR###`. Generated registries are named `{AssemblyName}.DecoratRHandlerRegistry`.
- **Primary constructors** and `sealed record` types are used throughout.
- **Fully qualified type names** with `global::` prefix in all generated code.
- **`StringBuilderExtensions.AppendIndentedLine`** for consistent 4-space indentation in emitted source.


## Testing

- **Framework:** xunit.v3 with AwesomeAssertions for fluent assertions.
- **Test base:** `GeneratorTestBase` provides `RunGenerator()` (single-assembly) and `RunTwoStageGenerator()` (cross-assembly metadata propagation).
- **Fixtures:** `TestSources` has reusable source snippets and builder methods (`Decorator()`, `HandlerOnly()`, `FullPath()`).
- **Pattern:** Snapshot-style — generate source, then assert on string content. Use `.FindSource("ClassName")` and `.GetDecoratorApplicationSection()` helpers.
