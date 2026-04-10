# DecoratR

A compile-time [Decorator Pattern](https://en.wikipedia.org/wiki/Decorator_pattern) for .NET. A Roslyn source generator
discovers your request handlers and decorators, then wires them into the DI container automatically. No mediator, no
runtime reflection, no pipelines.

## How It Works

You define request handlers (`IRequestHandler<TRequest, TResponse>`) and decorators. The source generator scans your
assemblies at compile time, finds all handlers and decorators, and emits an `AddDecoratR()` extension method that
registers everything in `IServiceCollection`.

When you resolve a handler from the container, you get the full decorated chain. Decorators are ordered by the `Order`
property on the `[Decorator]` attribute. **Lower values run first (outermost)**:

```
Request
  → [Order = 1] ExceptionHandling
    → [Order = 2] PerformanceLogging
      → [Order = 3] Validation
        → Handler
Response
```

## Packages

| Package                   | Purpose                                                                                                                                             |
|---------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| **DecoratR.Abstractions** | Interfaces and attributes (`IRequest`, `IRequestHandler<,>`, `[Decorator]`). Reference this in library projects that define handlers or decorators. |
| **DecoratR.Generator**    | The Roslyn source generator. Reference this as an analyzer in projects that need code generation.                                                   |

```bash
# Library / application layer (abstractions only)
dotnet add package DecoratR.Abstractions

# Host project (also needs the generator)
dotnet add package DecoratR.Generator
```

## Quick Start

### 1. Define a request

A request is any type that implements the marker interface `IRequest`:

```csharp
public sealed record GreetCommand(string Name) : IRequest;
```

### 2. Implement a handler

A handler implements `IRequestHandler<TRequest, TResponse>` for a specific request type:

```csharp
internal sealed class GreetCommandHandler(IGreetingRepository repository)
    : IRequestHandler<GreetCommand, string>
{
    public async ValueTask<TResponse> HandleAsync(
        GreetCommand command,
        CancellationToken cancellationToken = default)
    {
        var greeting = Greeting.Create(command.Name);
        await repository.AddAsync(greeting, cancellationToken);
        return greeting.Message;
    }
}
```

Handlers can be `internal` or `public`. They can inject any dependencies through the constructor. The generator
discovers them automatically, with no manual registration needed.

### 3. Write decorators

A decorator is an **open-generic** class that:

- Implements `IRequestHandler<TRequest, TResponse>`
- Is marked with `[Decorator]`
- Receives the inner `IRequestHandler<TRequest, TResponse>` via constructor injection

```csharp
[Decorator(Order = 1)]
public sealed class ExceptionHandlingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

Decorators can also inject additional services:

```csharp
[Decorator(Order = 2)]
public sealed class RequestLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<RequestLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {Request}: {Payload}", typeof(TRequest).Name, request);
        var response = await inner.HandleAsync(request, cancellationToken);
        logger.LogInformation("Handled {Request} → {Response}", typeof(TRequest).Name, response);
        return response;
    }
}
```

### 4. Configure the source generator

The generator supports **two assembly-level attributes** that control what gets generated:

#### `[GenerateDecoratRMetadata]` (for library projects)

Use this in projects that define handlers and/or decorators but are **not** the composition root. The generator emits
metadata that the composition root can discover at compile time.

```csharp
// In your application layer project
using DecoratR;

[assembly: GenerateDecoratRMetadata]
```

This generates:

- A `DecoratRHandlerRegistry` class listing all discovered handlers
- A `DecoratRDecoratorRegistry` class with apply methods for each decorator
- Assembly-level attributes so the composition root can find them across assembly boundaries

#### `[GenerateDecoratRRegistrations]` (for the composition root)

Use this in the host/startup project (e.g., your ASP.NET Core project). The generator scans the current assembly **and**
all referenced assemblies for handlers and decorators, then emits the `AddDecoratR()` extension method.

```csharp
// AssemblyInfo.cs in the host project
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

### 5. Register services

Call the generated `AddDecoratR()` method in your service configuration:

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddDecoratR(); // Generated at compile time

var app = builder.Build();
```

## Handler Lifetime

By default, all handlers are registered as **Transient**. You can change the service lifetime by passing a configuration
action to `AddDecoratR()`:

```csharp
builder.Services.AddDecoratR(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
});
```

This sets the lifetime for **all** handler registrations. Decorators automatically inherit the lifetime of the handler they wrap.

### 6. Use handlers

Inject `IRequestHandler<TRequest, TResponse>` anywhere you need it. The container returns the fully decorated chain:

```csharp
app.MapPost("/greet", async (
    IRequestHandler<GreetCommand, string> handler,
    GreetRequest request,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new GreetCommand(request.Name), cancellationToken);
    return Results.Ok(new { Message = result });
});
```

## Multi-Project Setup

A typical Clean Architecture setup looks like this:

```
MyApp.Domain/             → Domain entities (no DecoratR reference needed)
MyApp.Application/        → Handlers, decorators, abstractions
                            References: DecoratR.Abstractions + DecoratR.Generator (analyzer)
                            Assembly attribute: [GenerateDecoratRMetadata]

MyApp.Infrastructure/     → Repository implementations, external services
MyApp.Presentation/       → ASP.NET Core host, endpoint definitions
                            References: DecoratR.Generator (analyzer)
                            Assembly attribute: [GenerateDecoratRRegistrations]
```

The key rules:

1. **Library projects** that define handlers/decorators use `[GenerateDecoratRMetadata]` and reference both
   `DecoratR.Abstractions` and `DecoratR.Generator` (as analyzer).
2. **The composition root** (host project) uses `[GenerateDecoratRRegistrations]` and references `DecoratR.Generator` (
   as analyzer). It picks up handlers and decorators from all referenced assemblies automatically.
3. By default, decorators are applied to **every** handler across all assemblies.
   Use [type constraints](#scoping-decorators-to-specific-request-types) to limit a decorator to specific request types.

## Scoping Decorators to Specific Request Types

By default, a decorator with `where TRequest : IRequest` wraps every handler. To restrict a decorator to a subset of
requests, narrow the generic constraint to a more specific type:

```csharp
// Define marker interfaces for different request kinds
public interface ICommand : IRequest;
public interface IQuery : IRequest;

// Requests
public sealed record CreateUserCommand(string Name) : ICommand;
public sealed record GetUsersQuery : IQuery;

// This decorator only wraps ICommand handlers
[Decorator(Order = 2)]
public sealed class ValidationDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IValidator<TRequest> validator)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand          // ← only commands
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await inner.HandleAsync(request, cancellationToken);
    }
}
```

The generator reads the constraint at compile time and only emits `DecorateService` calls for handlers whose request
type satisfies the constraint. In the example above, `ValidationDecorator` wraps `CreateUserCommand` (which implements
`ICommand`) but **not** `GetUsersQuery`.

This works across assembly boundaries and supports any constraint expressible in C#: interfaces, base classes, or
combinations like `where TRequest : ICommand, ILoggable`.

## Decorator Ordering

Decorators wrap handlers from the outside in. **Lower `Order` values execute first** (outermost), meaning they see the
request before higher-order decorators:

| Order | Decorator                     | Position                             |
|-------|-------------------------------|--------------------------------------|
| 1     | `ExceptionHandlingDecorator`  | Outermost. Catches everything.       |
| 2     | `PerformanceLoggingDecorator` | Measures time of inner chain         |
| 3     | `RequestLoggingDecorator`     | Logs request/response                |
| 4     | `ValidationDecorator`         | Innermost. Validates before handler. |

When two decorators share the same `Order`, they are sorted alphabetically by name.

## Diagnostics

The generator reports diagnostics to help with discovery:

| Code      | Severity | Description                               |
|-----------|----------|-------------------------------------------|
| `DCTR001` | Info     | No handlers found in the current assembly |
| `DCTR002` | Info     | Lists all discovered handlers             |
| `DCTR003` | Info     | Lists all discovered decorators           |

## AOT Compatibility

DecoratR is designed to work with Native AOT.

## Requirements

- .NET 8+

## Examples

Working examples are available in the [`examples/`](examples/) directory:

| Example                                                | Description                                                                                                                                                                                |
|--------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [**Simple API**](examples/simple-api/)                 | A single-project minimal API that uses DecoratR with a logging decorator. The simplest way to get started.                                                                                 |
| [**Clean Architecture**](examples/clean-architecture/) | A multi-project CQRS setup with `IQuery`/`ICommand`/`IQueryHandler`/`ICommandHandler` abstractions layered on top of DecoratR, plus exception handling and performance logging decorators. |

Both examples use a [shared project](examples/shared/) containing an in-memory Todo repository seeded with fake data
via [Bogus](https://github.com/bchavez/Bogus).
