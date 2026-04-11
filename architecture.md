# Architecture

DecoratR is a compile-time decorator pipeline for .NET request handlers, built as a Roslyn incremental source generator.

## Project Map

```
src/
├── DecoratR.Abstractions/   Public API (IRequest, IRequestHandler, DecoratorAttribute)
│                             Multi-targets net8.0/net9.0/net10.0, AOT-compatible
│
└── DecoratR.Generator/      Roslyn incremental generator (netstandard2.0)
                              Shipped as analyzer; no runtime dependency

tests/
└── DecoratR.Generator.Tests/ Generator unit tests (xunit.v3 + AwesomeAssertions)

examples/
├── simple-api/               Single-project example (ASP.NET Core + handlers + decorator)
├── clean-architecture/       Multi-assembly example (application + API layers)
└── shared/                   Shared fixtures (in-memory repo, Todo model)

playground/                   Multi-layer dev sandbox (Presentation → Application → Infrastructure → Domain)
```

## Generator Pipeline

The generator has two activation modes, controlled by assembly-level attributes:

| Attribute | Where | What it produces |
|-----------|-------|------------------|
| `[GenerateDecoratRMetadata]` | Library projects | Handler registry, decorator registry, assembly-level metadata attributes |
| `[GenerateDecoratRRegistrations]` | Composition root / host | `AddDecoratR()` extension method with full DI wiring |

### Three-phase flow

```
1. Attribute Emission (always)
   └── Five bootstrap attributes emitted via PostInitialization

2. Detection
   ├── HandlerDetector     → scans for IRequestHandler<,> implementations (non-abstract, non-generic, non-decorator)
   ├── DecoratorDetector   → scans for [Decorator]-annotated open generics, extracts Order + constraints
   └── ReferencedAssemblyScanner → reads assembly-level metadata from referenced projects

3. Emission
   ├── HandlerRegistryEmitter      → DecoratRHandlerRegistry class + assembly attributes (metadata path)
   ├── DecoratorRegistryEmitter    → DecoratRDecoratorRegistry with per-decorator Apply methods
   └── FullRegistrationEmitter     → AddDecoratR() merging local + referenced handlers/decorators
```

### Cross-assembly composition

Library projects emit handler/decorator registries and assembly-level attributes. The composition root reads these via `ReferencedAssemblyScanner` and orchestrates the final `AddDecoratR()` method — no runtime reflection needed.

```
MyApp.Application  ──[GenerateDecoratRMetadata]──►  emits registry + assembly attributes
        │
        ▼  (project reference)
MyApp.Api  ──[GenerateDecoratRRegistrations]──►  reads metadata, emits AddDecoratR()
```

### Decorator ordering and constraint matching

- **Ordering:** Lower `Order` = outermost (runs first on entry, last on exit). Ties broken alphabetically by FQN.
- **Constraint filtering:** Decorators declare constraints on `TRequest` (e.g., `where TRequest : ICommand`). At generation time, each handler's request type hierarchy is matched against decorator constraints — only compatible decorators are wired up.
- **Type hierarchy serialization:** Request types and their implemented interfaces/bases are semicolon-delimited in assembly-level attributes to avoid JSON dependencies in netstandard2.0.

## Key Design Decisions

### AOT-first
No reflection at any point. All handler/decorator resolution is statically known. `DecoratR.Abstractions` sets `IsAotCompatible=true`; generated code includes `DynamicallyAccessedMembers` attributes.

### Incremental generator caching
All metadata types (`HandlerMetadata`, `DecoratorMetadata`, `ReferencedDecoratorInfo`) implement `IEquatable<>`. `EquatableArray<T>` wraps `ImmutableArray<T>` for structural equality, enabling Roslyn's incremental cache to skip redundant re-generation.

### netstandard2.0 for the generator
Roslyn analyzers/generators must target netstandard2.0. This constrains the generator to avoid modern C# APIs but ensures compatibility across all supported SDK versions.

### ValueTask-based handlers
`IRequestHandler<TRequest, TResponse>.HandleAsync` returns `ValueTask<TResponse>` — optimized for the common synchronous-completion path.

## Diagnostics

| Code | Severity | Meaning |
|------|----------|---------|
| `DCTR001` | Warning | No handlers or decorators found for the marked assembly |
| `DCTR002` | Info | Handlers discovered successfully |
| `DCTR003` | Info | Decorators discovered successfully |
