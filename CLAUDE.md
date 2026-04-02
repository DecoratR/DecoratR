# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Test all
dotnet test

# Run a single test (by filter)
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run tests for a specific project
dotnet test tests/DecoratR.Generator.Tests

# Run sample app
cd playground/DecoratR.Sample.Presentation && dotnet run
```

CI builds (`/p:CI=true`) treat warnings as errors.

## Architecture

DecoratR is a lightweight Decorator Pattern library for .NET. It wraps `IRequestHandler<TRequest, TResponse>` implementations with decorators for cross-cutting concerns — no mediator, no pipeline objects.

```
Request → Decorator(outermost) → ... → Decorator(innermost) → Handler
```

Decorators are registered in application order; the first registered becomes the outermost wrapper.

### Package Split

| Package | Purpose | Target |
|---|---|---|
| `DecoratR.Abstractions` | `IRequest`, `IRequestHandler<,>`, `[Decorator]` | net10.0 |
| `DecoratR` | DI registration, `AddDecoratR()`, `Decorate()` | net10.0 |
| `DecoratR.Generator` | Roslyn source generator | netstandard2.0 |
| `DecoratR.Reflection` | Assembly-scanning handler discovery | net10.0 |

Application layer depends on `DecoratR.Abstractions` only. Presentation/host layer wires up `DecoratR` + optionally `DecoratR.Generator` (as analyzer) and/or `DecoratR.Reflection`.

### Two Registration Paths

**Reflection (runtime):** Call `options.RegisterHandlersFromAssembly(assembly)` inside `AddDecoratR()`. Scans assemblies at startup for `IRequestHandler<,>` implementations.

**Source generator (compile-time):** Decorate the registration class with `[GenerateDecoratRRegistrations]`. The generator emits optimized DI registration code — no runtime reflection. Cross-assembly handler discovery uses generated `[DecoratRHandlerRegistration]` / `[DecoratRHandlerServiceType]` assembly-level attributes. There is also a lighter `[GenerateHandlerRegistrations]` attribute that emits only a `HandlerRegistry.Handlers` list.

### Source Generator

`DecoratRIncrementalGenerator` (`IIncrementalGenerator`) scans syntax trees for:
- **Handlers:** non-abstract, non-static, non-open-generic classes implementing `IRequestHandler<,>` that do **not** have `[Decorator]`
- **Decorators:** open-generic classes annotated with `[Decorator(Order = N)]`
- **Cross-assembly registrations:** methods marked `[DecoratRRegistrationMethod]`

Key types: `HandlerMetadata`, `DecoratorMetadata`, `RegistrationMethodMetadata`, `SourceGenerationHelper`, `StringBuilderExtensions`.

### Decorator Ordering

Decorators are ordered by `[Decorator(Order = N)]`. Lower order = outermost (runs first in the pipeline). Same order uses alphabetical/registration order as tiebreaker.

**Reflection path:** `RegisterDecoratorsFromAssembly(assembly)` discovers `[Decorator]` types from assemblies. `AddDecorator(Type, order)` is available for manual registration.
