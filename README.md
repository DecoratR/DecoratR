# DecoratR

DecoratR is a compile-time decorator pipeline for .NET request handlers.

It uses a Roslyn source generator to find your handlers and decorators at build time and generates a single `AddDecoratR()` extension method for DI registration. That gives you ordered decorator chains without a mediator, runtime scanning, or reflection-based registration.

## Why use it

- Keep handler code focused on business logic.
- Move logging, validation, timing, and exception handling into decorators.
- Register handlers automatically at compile time.
- Support multi-project solutions without manual DI wiring.
- Stay friendly to AOT and trimming scenarios.

## The mental model

DecoratR has three core concepts:

- `IRequest` is the message.
- `IRequestHandler<TRequest, TResponse>` handles that message.
- `[Decorator]` wraps handlers with cross-cutting behavior.

At runtime, you resolve a handler from DI and get the fully wrapped chain:

```text
Request
  -> [Order = 1] ExceptionHandlingDecorator
    -> [Order = 2] PerformanceLoggingDecorator
      -> [Order = 3] ValidationDecorator
        -> Actual handler
Response
```

Lower `Order` values are outermost, so they run first on the way in and last on the way out.

## Packages

| Package | Purpose |
| --- | --- |
| `DecoratR.Abstractions` | Interfaces and attributes used by handlers and decorators. |
| `DecoratR.Generator` | Source generator that emits registration code. |


## Quick start

This is the simplest setup: one project contains the handlers, decorators, and application startup.

### 1. Define a request

```csharp
using DecoratR;

public sealed record GetGreetingQuery(string Name) : IRequest;
```

### 2. Implement a handler

```csharp
using DecoratR;

internal sealed class GetGreetingQueryHandler
    : IRequestHandler<GetGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GetGreetingQuery request,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult($"Hello, {request.Name}");
    }
}
```

Handlers can be `public` or `internal`. Constructor injection works as normal.

### 3. Add a decorator

Decorators must:

- be open generic
- implement `IRequestHandler<TRequest, TResponse>`
- be marked with `[Decorator]`
- accept the inner `IRequestHandler<TRequest, TResponse>` through constructor injection

```csharp
using DecoratR;
using Microsoft.Extensions.Logging;

[Decorator(Order = 1)]
public sealed class LoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<LoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);

        var response = await inner.HandleAsync(request, cancellationToken);

        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

### 4. Enable code generation

In the host project, add this assembly attribute:

```csharp
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

You can place it in `AssemblyInfo.cs` or any `.cs` file in that project.

### 5. Register DecoratR

`AddDecoratR()` is generated at compile time for the host assembly.

```csharp
using MyApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();

var app = builder.Build();
```

If the generated extension method is not in scope, import your host assembly namespace, for example `using MyApp;`.

### 6. Use the handler

```csharp
app.MapGet("/greeting/{name}", async (
    string name,
    IRequestHandler<GetGreetingQuery, string> handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(new GetGreetingQuery(name), cancellationToken);
    return Results.Ok(new { message = result });
});
```

That injected handler is the decorated chain, not just the raw handler implementation.

## Single-project vs multi-project setup

DecoratR supports both simple apps and layered solutions.

| Scenario | What to reference | Which attribute to add |
| --- | --- | --- |
| Single project app | `DecoratR.Abstractions` and `DecoratR.Generator` | `[assembly: GenerateDecoratRRegistrations]` |
| Class library that defines handlers or decorators | `DecoratR.Abstractions` and `DecoratR.Generator` | `[assembly: GenerateDecoratRMetadata]` |
| Composition root / host project | `DecoratR.Generator` | `[assembly: GenerateDecoratRRegistrations]` |

### Multi-project example

```text
MyApp.Domain/
MyApp.Application/     handlers, decorators
MyApp.Infrastructure/  repositories, external services
MyApp.Api/             ASP.NET Core host
```

Use this split:

- `MyApp.Application` references `DecoratR.Abstractions` and `DecoratR.Generator`, and adds `[assembly: GenerateDecoratRMetadata]`.
- `MyApp.Api` references `DecoratR.Generator`, references `MyApp.Application`, and adds `[assembly: GenerateDecoratRRegistrations]`.
- `MyApp.Api` calls `builder.Services.AddDecoratR()`.

The host generator reads metadata from referenced assemblies and generates registrations for the whole graph.

## Decorator behavior

### Ordering

- Lower `Order` values are applied first and become the outermost decorators.
- Higher `Order` values are closer to the handler.
- If two decorators have the same `Order`, DecoratR sorts them alphabetically by fully qualified name.

### Scoping decorators to specific request types

By default, a decorator constrained as `where TRequest : IRequest` wraps every handler.

To target only some requests, use a more specific constraint:

```csharp
public interface ICommand : IRequest;
public interface IQuery : IRequest;

public sealed record CreateUserCommand(string Name) : ICommand;
public sealed record GetUsersQuery() : IQuery;

[Decorator(Order = 2)]
public sealed class ValidationDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IValidator<TRequest> validator)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await inner.HandleAsync(request, cancellationToken);
    }
}
```

In that example, the decorator applies to command handlers only.

This works across assembly boundaries because the generator records request type metadata at compile time.

## Handler lifetime

Handlers are registered as `Transient` by default.

You can override that for all generated registrations:

```csharp
builder.Services.AddDecoratR(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
});
```

Decorators inherit the lifetime of the handler they wrap.

## What gets generated

DecoratR generates different code depending on where you enable it.

### In library projects marked with `[GenerateDecoratRMetadata]`

DecoratR emits:

- a handler registry
- a decorator registry
- assembly-level metadata used by the host project

### In the host project marked with `[GenerateDecoratRRegistrations]`

DecoratR emits:

- `AddDecoratR()`
- `DecoratROptions`
- the DI registration code that wires handlers and decorators together

You do not manually register each handler or decorator.

## Diagnostics

The generator reports a small set of diagnostics to help with discovery and troubleshooting.

| Code | Severity | Meaning |
| --- | --- | --- |
| `DCTR001` | Warning | No handlers or decorators were found for the marked assembly. |
| `DCTR002` | Info | Handlers were discovered successfully. |
| `DCTR003` | Info | Decorators were discovered successfully. |

## Requirements

- Repository development and the included samples use .NET SDK `10.0.0` as pinned in `global.json`.
- `DecoratR.Abstractions` targets `net8.0`, `net9.0`, and `net10.0`.
- `DecoratR.Generator` targets `netstandard2.0` as a Roslyn analyzer.

## Examples in this repository

### Simple API

The fastest example to read. It shows a minimal ASP.NET Core app with handlers, a decorator, and generated registration.

### Clean Architecture

Shows the cross-assembly setup with metadata generation in the application layer and full registration generation in the API project.

## License

This project is licensed under the [MIT License](LICENSE).