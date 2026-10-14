# Architecture

DecoratR is a compile-time decorator pipeline for .NET request handlers, built as a Roslyn incremental source generator.

## Project Map

```
src/
├── DecoratR.Abstractions/    Public API: IRequest, IRequestHandler<,>, IRequestHandler<> (no response, bridges
│                              to IRequestHandler<TRequest, Unit>), Unit, VoidRequestHandler<> (generated facade
│                              registration), IStreamRequest, IStreamRequestHandler,
│                              DecoratorAttribute, DecoratROptions, the two trigger attributes
│                              ([GenerateDecoratRMetadata], [GenerateDecoratRRegistrations]) and the
│                              DecoratR.Metadata infrastructure: the attributes that carry cross-assembly
│                              metadata, HandlerRegistration (registry entry with generated factory) and
│                              DecoratRServiceKeys (key of the validation registrations).
│                              Multi-targets net8.0/net9.0/net10.0, AOT-compatible, depends on
│                              Microsoft.Extensions.DependencyInjection.Abstractions.
│
└── DecoratR.Generator/       Roslyn incremental generator (netstandard2.0), shipped as analyzer.
    ├── Detection/            Symbol inspection: HandlerDetector, DecoratorDetector, ConstructorAnalyzer,
    │                          ConstraintSerializer, TypeHierarchy, ReferencedAssemblyScanner, SymbolExtensions
    ├── Model/                Equatable pipeline values (records): HandlerMetadata, DecoratorMetadata,
    │                          ConstructorPlan, ServiceTypeInfo, ReferencedRegistrationData, DiagnosticInfo, …
    ├── Emit/                 SourceWriter plus the emitters: HandlerRegistryEmitter, DecoratorRegistryEmitter,
    │                          RegistrationsEmitter, FactoryEmitter, RuntimeHelpersEmitter, DecorationPlanner,
    │                          ConstraintMatcher
    ├── Diagnostics.cs        DCTR001–DCTR013 descriptors (tracked in AnalyzerReleases.*.md)
    └── DecoratRIncrementalGenerator.cs   Pipeline wiring and output steps

tests/
├── DecoratR.Generator.Tests/      Generator tests (xunit.v3 + AwesomeAssertions). Every run compiles the
│                                   generated code; Snapshots/ holds full-output snapshots.
├── DecoratR.IntegrationTests/     Runs the generated AddDecoratR() against a real ServiceProvider,
│   └── …Tests.Library/            with a referenced library that uses [GenerateDecoratRMetadata].
├── DecoratR.Generator.Benchmarks/ BenchmarkDotNet benchmarks of the generator itself, including a modular
│                                   monolith scenario (N modules × M handlers, cold run and IDE keystroke)
└── DecoratR.Runtime.Benchmarks/   BenchmarkDotNet benchmarks of the generated code: resolving a decorated
                                    pipeline and the registration cost of AddDecoratR() for 100–2000 handlers

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
- **Handlers without a response** implement `IRequestHandler<TRequest>`, which derives from
  `IRequestHandler<TRequest, Unit>`, so detection, constraint matching and cross-assembly metadata see the
  ordinary two-parameter service type. `HandlerMetadata.RegistersVoidFacade` marks them, and the emitters add a
  second registration `IRequestHandler<TRequest>` → `VoidRequestHandler<TRequest>` (in `AddDecoratR()` for local
  handlers, in the library's `Handlers` array for referenced ones). The facade is never a decoration target.
- **Decorators**: `[Decorator]` classes that are open generic with exactly two type parameters used as the
  `TRequest`/`TResponse` arguments of exactly one handler interface (the declared order of the type parameters
  does not matter). Constraints on both type parameters are captured, including `class`, `struct`, `notnull`,
  `unmanaged`, `new()` and generic constraints such as `where TRequest : IQuery<TResponse>`.
- **Constructors** (`ConstructorAnalyzer` → `ConstructorPlan`): a handler or decorator is constructed directly
  by generated code when it has exactly one public constructor (or exactly one marked
  `[ActivatorUtilitiesConstructor]`) whose parameters are services (`GetRequiredService<T>`),
  `[FromKeyedServices(key)]` services with a constant key, the `IServiceProvider` or, for decorators, exactly one
  inner handler. Everything else (several constructors, optional, `params`, `ref`/`in`/`out` or `[ServiceKey]`
  parameters, keys that are not constant, unset `required` members) silently keeps the `ActivatorUtilities` path:
  the plan is `null`, handlers are registered by implementation type, decorators go through `Wrap`.

### Cross-assembly composition

Library projects emit registries and three kinds of assembly attributes (`DecoratR.Metadata` namespace):

- `[DecoratRRegistry("global::Lib.DecoratRHandlerRegistry")]`
- `[DecoratRHandler(handlerType, requestType, responseType, isStream, RequestTypeHierarchy = …, ResponseTypeHierarchy = …, IsPubliclyAccessible = …)]`
- `[DecoratRDecorator(applyMethod, decoratorType, order, isStream, RequestConstraints = …, ResponseConstraints = …)]`

Type hierarchies are semicolon-delimited lists of fully qualified type names (the type, its public interfaces,
its base types) plus type facts (`!class`, `!struct`, `!unmanaged`, `!new`). Constraints use the same encoding
with the decorator's own type parameters replaced by `{TRequest}`/`{TResponse}` placeholders, which the
composition root substitutes before matching. The composition root only ever references a library's
`DecoratRHandlerRegistry.Handlers` and its `DecoratRDecoratorRegistry.ApplyXxx<TRequest, TResponse>(ServiceDescriptor)`
methods, so internal handlers and decorators work across projects. Request and response types must be public
for cross-assembly decoration (DCTR011/DCTR013), and so must the constraint types of exported decorators (DCTR014).

`Handlers` is a `ReadOnlySpan<DecoratR.Metadata.HandlerRegistration>` over a private array; every entry carries the
service type, the implementation type and the generated factory (`static sp => new Handler(…)`, `null` for the
`ActivatorUtilities` fallback). Registries emitted by earlier generator versions declare their own
`HandlerRegistration` record without factories; `ReferencedAssemblyScanner` tells both apart by the type of the
`Handlers` property, and the composition root registers the handlers of such registries by implementation type.

### Registration and decorator application at runtime

`AddDecoratR()` runs in four steps:

1. **Idempotency.** If the private `Marker` type is already registered, it returns immediately; otherwise it
   registers it. A second call would register every handler again and decorate the first registrations twice.
2. **Handlers.** Each handler is registered through its generated factory (`ServiceDescriptor.Describe`), local
   handlers inline, referenced ones through `AddHandlers(services, Registry.Handlers, lifetime)`.
3. **Validation registrations.** For every handler, a keyed registration
   `(implementationType, DecoratRServiceKeys.Validation, implementationType, lifetime)` is added. It is never resolved;
   `ValidateOnBuild`/`ValidateScopes` build its call site and report missing dependencies and captive dependencies,
   which they cannot see through factory registrations. Resolution itself stays on the generated factory, which is
   faster than resolving the handler through the container, especially under NativeAOT.
4. **Decoration in one pass.** The generated code fills a `Dictionary<Type, Pipeline>` (one entry per decorated
   service type, innermost decorator first) and `Decorate` walks the service collection once, replacing every
   non-keyed registration of a decorated service type, including registrations the user added before. Service
   types without any registration are reported together at the end.

Each pipeline step calls an apply method: the public one of a referenced `DecoratRDecoratorRegistry` or a private
one emitted into the composition root for local decorators. Apply methods construct the decorator with `new`
around the inner factory and keep the lifetime of the wrapped registration; resolving a pipeline therefore costs one
delegate call and one allocation per layer, without `object[]` arguments or reflection. Handler factories and apply
methods create delegates typed with the handler interface (`Func<IServiceProvider, IRequestHandler<TRequest, TResponse>>`);
an apply method checks once whether the inner factory has that type and then calls it without an interface cast
(registrations made elsewhere are cast per resolution). This saves about 5 ns per resolution of a three-layer pipeline
at the price of one delegate type per service type at startup. Only decorators
without a constructor plan use `Wrap`, and only registrations by implementation type go through
`ActivatorUtilities`; both create their `ObjectFactory` lazily on the first resolution instead of in `AddDecoratR()`.
The helpers are private and emitted into every registry and composition root, so each assembly is self-contained.

### Runtime cost

Measured with `tests/DecoratR.Runtime.Benchmarks` and NativeAOT console probes (Apple M4 Pro, .NET 10.0.12; details,
methodology and the comparison with the previous `ActivatorUtilities`-based code in
`docs/modular-monolith-analysis.md`, section 4):

| | JIT | NativeAOT |
|---|---:|---:|
| Resolve a transient pipeline with three decorators | 57 ns · 120 B (hand-written factory: 48 ns) | 91 ns · 120 B (hand-written: 89 ns) |
| `AddDecoratR()`, 500 handlers × 3 decorators, warm | 70 µs · 406 KB | – |
| `AddDecoratR()`, 500 handlers × 3 decorators, first call in a fresh process | 30 ms | 0.17 ms |

The first-call cost under JIT is dominated by compiling one pipeline lambda and the apply method instantiations per
service type.

## Key Design Decisions

- **AOT-first.** Handlers and decorators are constructed by generated code; reflection (`ActivatorUtilities`) is
  limited to the fallback for constructors the generator cannot call directly and to registrations by
  implementation type. All types are statically known, the fallback carries `DynamicallyAccessedMembers`
  annotations. The integration test project builds with `IsAotCompatible=true`.
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
