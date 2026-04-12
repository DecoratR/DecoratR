# DecoratR Guide

DecoratR creates compile time decorator pipelines for .NET request handlers.

If you want the short package overview first, read the README at [https://github.com/DecoratR/DecoratR/blob/main/README.md](https://github.com/DecoratR/DecoratR/blob/main/README.md).

## What Problem DecoratR Solves

In many applications, request handlers start simple and then accumulate logging, validation, timing, retries, and exception handling. Manual DI registration makes that worse because every extra behavior must be wired by hand.

DecoratR keeps the handler surface small.

1. Handlers contain business logic.
2. Decorators contain cross cutting behavior.
3. A source generator discovers everything during the build.
4. The host project receives a generated `AddDecoratR()` method that registers handlers and wraps them in the right order.

This means you get deterministic pipelines without a mediator dependency, without runtime assembly scanning, and without reflection based registration.

## Package Model

DecoratR is split into two packages.

1. [`DecoratR.Abstractions`](https://www.nuget.org/packages/DecoratR.Abstractions) contains the interfaces and attributes used in application code.
2. [`DecoratR.Generator`](https://www.nuget.org/packages/DecoratR.Generator) contains the source generator that emits registrations and cross assembly metadata.

In a typical application project that defines handlers or decorators, install both packages.

```bash
dotnet add package DecoratR.Abstractions
dotnet add package DecoratR.Generator
```

In a composition root that only aggregates referenced assemblies, install `DecoratR.Generator`.

```bash
dotnet add package DecoratR.Generator
```

## Core Concepts

### Requests and handlers

The request response pipeline uses `IRequest` and `IRequestHandler<TRequest, TResponse>`.

```csharp
using DecoratR;

public sealed record GetGreetingQuery(string Name) : IRequest;

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

Handlers can be `public` or `internal`. Constructor injection works like any other DI based service.

### Decorators

Decorators wrap handlers and apply cross cutting behavior.

DecoratR discovers a class as a decorator when all of the following are true.

1. The class is marked with `[Decorator]`.
2. The class is open generic.
3. The class implements the matching handler interface.
4. The constructor accepts the inner handler instance.

```csharp
using DecoratR;
using Microsoft.Extensions.Logging;

[Decorator(Order = 1)]
internal sealed class LoggingDecorator<TRequest, TResponse>(
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

### Stream requests and stream handlers

The stream pipeline uses `IStreamRequest` and `IStreamRequestHandler<TRequest, TResponse>`.

```csharp
using DecoratR;

public sealed record GetItemsStreamQuery(string? Filter = null) : IStreamRequest;

internal sealed class GetItemsStreamQueryHandler(IItemRepository repository)
    : IStreamRequestHandler<GetItemsStreamQuery, string>
{
    public async IAsyncEnumerable<string> HandleAsync(
        GetItemsStreamQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in repository.GetAllAsync(cancellationToken))
        {
            yield return item.Name;
        }
    }
}
```

Stream decorators follow the same model, but they implement `IStreamRequestHandler<TRequest, TResponse>`.

## Single Project Setup

Use this setup when the same project contains requests, handlers, decorators, and startup code.

### 1. Install the packages

```bash
dotnet add package DecoratR.Abstractions
dotnet add package DecoratR.Generator
```

### 2. Add the assembly attribute

Place the attribute in `AssemblyInfo.cs` or any `.cs` file in the host project.

```csharp
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

### 3. Register DecoratR

```csharp
using MyApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
```

`AddDecoratR()` is generated in the host assembly namespace. If the method is not in scope, import the host assembly namespace.

### 4. Resolve handlers from DI

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

The resolved handler is the decorated chain, not just the raw implementation type.

## Multi Project Setup

Use this setup when handlers and decorators live in one or more class libraries and a separate host project composes the application.

### 1. Mark library projects with metadata generation

Every project that declares handlers or decorators should reference both packages and include this attribute.

```csharp
using DecoratR;

[assembly: GenerateDecoratRMetadata]
```

That causes DecoratR to emit handler metadata, decorator metadata, and generated registry helpers for that assembly.

### 2. Mark the composition root with registration generation

The host project references `DecoratR.Generator` and includes this attribute.

```csharp
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

That host project receives `AddDecoratR()` and merges local and referenced metadata into one registration method.

### 3. Call `AddDecoratR()` in the host

```csharp
using MyHost;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
```

### 4. Understand what crosses assembly boundaries

DecoratR propagates enough compile time metadata to let the host compose the final pipeline.

1. Request handlers discovered in library projects are exposed through generated registries.
2. Decorators discovered in library projects are exposed through generated apply methods.
3. Request type hierarchies are serialized so constrained decorators can still match the right handlers in the host.

This is what makes internal handlers and internal decorators usable across project boundaries without hand written DI code.

## Decorator Ordering

Ordering is deterministic.

1. Lower `Order` values are outermost.
2. Higher `Order` values are closer to the handler.
3. If two decorators share the same `Order`, DecoratR sorts them alphabetically by fully qualified type name.

Example:

```csharp
[Decorator(Order = 1)]
internal sealed class ExceptionHandlingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<ExceptionHandlingDecorator<TRequest, TResponse>> logger)
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
            logger.LogError(ex, "Unhandled exception handling {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}

[Decorator(Order = 2)]
internal sealed class ValidationDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IValidator<TRequest> validator)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
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

The execution flow becomes:

```text
Request -> ExceptionHandlingDecorator -> ValidationDecorator -> Handler -> ValidationDecorator -> ExceptionHandlingDecorator -> Response
```

## Constrained Decorators

Decorators can target specific request families through generic constraints.

```csharp
public interface ICommand : IRequest;
public interface IQuery : IRequest;

public sealed record CreateUserCommand(string Name) : ICommand;
public sealed record GetUsersQuery() : IQuery;

[Decorator(Order = 1)]
internal sealed class CommandLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<CommandLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling command {RequestType}", typeof(TRequest).Name);
        return await inner.HandleAsync(request, cancellationToken);
    }
}
```

In that example, the decorator applies to command handlers only.

DecoratR matches constraints against the full request type hierarchy.

1. The request type itself is considered.
2. Implemented interfaces are considered.
3. Base types are considered.

This means a decorator constrained to an interface such as `ICommand` will still apply when a request implements that interface indirectly.

## Stream Pipeline

The stream pipeline is separate from the request response pipeline.

1. Stream handlers implement `IStreamRequestHandler<TRequest, TResponse>`.
2. Stream decorators implement the same interface.
3. Regular decorators never wrap stream handlers.
4. Stream decorators never wrap regular handlers.

Example:

```csharp
using System.Runtime.CompilerServices;
using DecoratR;
using Microsoft.Extensions.Logging;

[Decorator(Order = 1)]
public sealed class StreamLoggingDecorator<TRequest, TResponse>(
    IStreamRequestHandler<TRequest, TResponse> inner,
    ILogger<StreamLoggingDecorator<TRequest, TResponse>> logger)
    : IStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamRequest
{
    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting stream for {RequestType}", typeof(TRequest).Name);

        await foreach (var item in inner.HandleAsync(request, cancellationToken))
        {
            yield return item;
        }

        logger.LogInformation("Completed stream for {RequestType}", typeof(TRequest).Name);
    }
}
```

## Lifetime and DI Behavior

`AddDecoratR()` accepts an optional configuration callback.

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddDecoratR(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
});
```

Important behavior:

1. The default lifetime is `Transient`.
2. Local handlers are registered directly in the generated host method.
3. Referenced handlers are registered through generated registries from their source assemblies.
4. Decorators inherit the lifetime of the wrapped handler.
5. The generated decorator chain uses `ActivatorUtilities.CreateInstance`, so normal constructor injection still works.

## What DecoratR Generates

The generated output depends on which assembly attribute you use.

### `GenerateDecoratRMetadata`

Library projects marked with `GenerateDecoratRMetadata` receive generated metadata artifacts.

1. A handler registry for request handlers and stream handlers.
2. A decorator registry with public apply methods.
3. Assembly level attributes that describe handler service types and decorator constraints.

### `GenerateDecoratRRegistrations`

Host projects marked with `GenerateDecoratRRegistrations` receive runtime registration code.

1. `AddDecoratR()`
2. `DecoratROptions`
3. Registration logic for local handlers
4. Registration logic for referenced handlers
5. Decorator application logic for request response and stream pipelines

You do not need to manually register each handler or decorator.

## Diagnostics

DecoratR reports a small set of diagnostics.

1. `DCTR001` warns that an assembly marked with `GenerateDecoratRMetadata` contains no handlers or decorators.
2. `DCTR002` reports how many handlers were discovered.
3. `DCTR003` reports how many decorators were discovered.

These diagnostics are mainly useful when a project compiles but the generated registrations are not what you expected.

## Troubleshooting

### `AddDecoratR()` is missing

Check the following.

1. The host project references `DecoratR.Generator`.
2. The host project contains `[assembly: GenerateDecoratRRegistrations]`.
3. The generated extension method namespace is imported.
4. The project actually builds with the generator enabled.

### A decorator is not applied

Check the following.

1. The decorator is marked with `[Decorator]`.
2. The decorator type is open generic.
3. The decorator implements the correct interface for the pipeline it targets.
4. The decorator constructor accepts the inner handler.
5. The generic constraint matches the request type hierarchy.
6. In a multi project setup, the assembly that defines the decorator is marked with `[assembly: GenerateDecoratRMetadata]`.

### A handler is not discovered

Check the following.

1. The handler is a concrete class.
2. The handler is not abstract, static, or generic.
3. The handler implements `IRequestHandler<TRequest, TResponse>` or `IStreamRequestHandler<TRequest, TResponse>`.
4. The project that owns the handler has the correct assembly attribute.

### Stream decorators do not affect regular handlers

This is expected. The pipelines are intentionally separate.

## Example Projects

1. Simple API example: [https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api](https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api)
2. Clean Architecture example: [https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture](https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture)
3. Playground sample: [https://github.com/DecoratR/DecoratR/tree/main/playground](https://github.com/DecoratR/DecoratR/tree/main/playground)
