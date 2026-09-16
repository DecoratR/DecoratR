# Architecture

DecoratR is a compile-time decorator pipeline for .NET request handlers, built as a Roslyn incremental source generator.

## Project Map

```
src/
├── DecoratR.Abstractions/    Public API: IRequest, IRequestHandler, IStreamRequest, IStreamRequestHandler,
│                              DecoratorAttribute, DecoratROptions, the two trigger attributes
│                              ([GenerateDecoratRMetadata], [GenerateDecoratRRegistrations]) and the
│                              DecoratR.Metadata attributes that carry cross-assembly metadata.
│                              Multi-targets net8.0/net9.0/net10.0, AOT-compatible, depends on
│                              Microsoft.Extensions.DependencyInjection.Abstractions.
│
└── DecoratR.Generator/       Roslyn incremental generator (netstandard2.0), shipped as analyzer.
    ├── Detection/            Symbol inspection: HandlerDetector, DecoratorDetector, ConstraintSerializer,
    │                          TypeHierarchy, ReferencedAssemblyScanner, SymbolExtensions
    ├── Model/                Equatable pipeline values (records): HandlerMetadata, DecoratorMetadata,
    │                          ServiceTypeInfo, ReferencedRegistrationData, DiagnosticInfo, LocationInfo, …
    ├── Emit/                 SourceWriter plus the emitters: HandlerRegistryEmitter, DecoratorRegistryEmitter,
    │                          RegistrationsEmitter, RuntimeHelpersEmitter, DecorationPlanner, ConstraintMatcher
    ├── Diagnostics.cs        DCTR001–DCTR013 descriptors (tracked in AnalyzerReleases.*.md)
    └── DecoratRIncrementalGenerator.cs   Pipeline wiring and output steps

tests/
├── DecoratR.Generator.Tests/      Generator tests (xunit.v3 + AwesomeAssertions). Every run compiles the
│                                   generated code; Snapshots/ holds full-output snapshots.
├── DecoratR.IntegrationTests/     Runs the generated AddDecoratR() against a real ServiceProvider,
│   └── …Tests.Library/            with a referenced library that uses [GenerateDecoratRMetadata].
└── DecoratR.Generator.Benchmarks/ BenchmarkDotNet benchmarks of the generator itself

examples/                          simple-api and clean-architecture consume the published NuGet packages;
                                   modular-monolith references src/ directly (two Clean Architecture modules + API)
playground/                        Multi-layer dev sandbox using project references (AOT enabled)
```

## Generator Pipeline

The generator has two activation modes, controlled by assembly-level attributes from `DecoratR.Abstractions`:

| Attribute | Where | What it produces |
|-----------|-------|------------------|
| `[GenerateDecoratRMetadata]` | Library projects | `DecoratRHandlerRegistry`, `DecoratRDecoratorRegistry`, assembly-level metadata attributes |
| `[GenerateDecoratRRegistrations]` | Composition root / host | `AddDecoratR()` in `Microsoft.Extensions.DependencyInjection` |

Both can be present in the same assembly.

### Incremental steps

```
CompilationProvider ──► AssemblyInfo (sanitized namespace)      ──┐
                    ──► HasServiceCollection (DI reference?)     ──┤
SyntaxProvider      ──► HandlerDetector  ──► Collect ──► HandlerSet ──┤   handlers of both kinds, deduplicated
ForAttribute(...)   ──► DecoratorDetector ─► Collect ──► DecoratorSet ┤   validated, sorted by (Order, type name)
ForAttribute(...)   ──► metadata trigger / registrations trigger  ──┤   presence + location for DCTR001
registrations trigger + Compilation ──► ReferencedAssemblyScanner ──┘   only scanned in composition roots
                                                                    │
                        ┌───────────────────────────────────────────┴───────────────┐
                        ▼                                                           ▼
             MetadataInput → EmitMetadata                             RegistrationsInput → EmitRegistrations
             (registries + DCTR002/003/007/013)                       (DecorationPlanner → AddDecoratR(),
                                                                       DCTR001/002/003/007/009/011)
             DetectionDiagnosticsInput → DCTR004/005/006/008/010/012 (only when a trigger is present)
```

All values flowing between steps are records with structural equality (`EquatableArray<T>` for collections,
`LocationInfo` instead of `Location`), so unrelated edits leave every output step cached. Tests in
`IncrementalCachingTests` assert this.

### Detection rules

- **Handlers**: non-abstract, non-static, non-generic classes or records (not nested in generic types) that
  implement `IRequestHandler<,>` or `IStreamRequestHandler<,>`. A class implementing several handler
  interfaces yields one registration per interface. Partial declarations are processed once. Handlers must be
  accessible from generated code (public or internal). Value types are rejected (DCTR010).
- **Decorators**: `[Decorator]` classes that are open generic with exactly two type parameters used as the
  `TRequest`/`TResponse` arguments of exactly one handler interface (the declared order of the type parameters
  does not matter). Constraints on both type parameters are captured, including `class`, `struct`, `notnull`,
  `unmanaged`, `new()` and generic constraints such as `where TRequest : IQuery<TResponse>`.

### Cross-assembly composition

Library projects emit registries and three kinds of assembly attributes (`DecoratR.Metadata` namespace):

- `[DecoratRRegistry("global::Lib.DecoratRHandlerRegistry")]`
- `[DecoratRHandler(handlerType, requestType, responseType, isStream, RequestTypeHierarchy = …, ResponseTypeHierarchy = …, IsPubliclyAccessible = …)]`
- `[DecoratRDecorator(applyMethod, decoratorType, order, isStream, RequestConstraints = …, ResponseConstraints = …)]`

Type hierarchies are semicolon-delimited lists of fully qualified type names (the type, its public interfaces,
its base types) plus type facts (`!class`, `!struct`, `!unmanaged`, `!new`). Constraints use the same encoding
with the decorator's own type parameters replaced by `{TRequest}`/`{TResponse}` placeholders, which the
composition root substitutes before matching. The composition root only ever references a library's
`DecoratRHandlerRegistry.Handlers` array and its `DecoratRDecoratorRegistry.ApplyXxx<TRequest, TResponse>(ServiceDescriptor)`
methods, so internal handlers and decorators work across projects. Request and response types must be public
for cross-assembly decoration (DCTR011/DCTR013), and so must the constraint types of exported decorators (DCTR014).

### Decorator application at runtime

For each service type the generated code calls `Decorate(services, serviceType, descriptor => …)`, which
replaces every non-keyed registration of the service type with a descriptor built by chaining `Wrap<TService, TDecorator>`
calls (innermost decorator first). `Wrap` uses `ActivatorUtilities.CreateFactory` once per registration, so
constructor selection is not repeated on every resolution, and it preserves the lifetime of the wrapped
registration. The helpers are private and emitted into every registry and composition root, so each assembly
is self-contained.

## Key Design Decisions

- **AOT-first.** No reflection beyond `ActivatorUtilities`, all types are statically known, generated code
  carries `DynamicallyAccessedMembers` annotations. The integration test project builds with
  `IsAotCompatible=true`.
- **Fail at build time.** Everything that used to produce invalid generated code or silently misbehaving
  registrations is now a diagnostic (see below).
- **Roslyn version pin.** The generator references the Roslyn version of the .NET 10 SDK baseline (5.0.0).
  A newer reference would make older SDKs skip the generator (CS9057); Renovate is configured not to bump it.
- **netstandard2.0 generator with records.** `Polyfills/IsExternalInit.cs` enables records and `init`.

## Diagnostics

| Code | Severity | Meaning |
|------|----------|---------|
| `DCTR001` | Warning | Trigger attribute present but no handlers or decorators found |
| `DCTR002` | Hidden | Handlers discovered (count) |
| `DCTR003` | Hidden | Decorators discovered (count) |
| `DCTR004` | Error | Decorator does not implement exactly one handler interface |
| `DCTR005` | Error | Decorator type parameters do not map to the handler interface |
| `DCTR006` | Warning | Decorator ignored (not generic, abstract, static, nested in generic type) |
| `DCTR007` | Error | Multiple handlers for the same service type |
| `DCTR008` | Warning | Handler or decorator not accessible from generated code |
| `DCTR009` | Error | Composition root does not reference Microsoft.Extensions.DependencyInjection.Abstractions |
| `DCTR010` | Warning | Handler is a value type |
| `DCTR011` | Warning | Decorators skipped for a referenced service type with non-public request/response type |
| `DCTR012` | Warning | Decorator has no public constructor accepting the inner handler |
| `DCTR013` | Info | Library handler whose request/response type is not public |
| `DCTR014` | Warning | Library decorator with a non-public constraint type is not exported |
