# DecoratR Guide

DecoratR creates compile time decorator pipelines for .NET request handlers.

If you want the short package overview first, read the README at [https://github.com/DecoratR/DecoratR/blob/main/README.md](https://github.com/DecoratR/DecoratR/blob/main/README.md).

## What Problem DecoratR Solves

In many applications, request handlers start simple and then accumulate logging, validation, timing, retries, and exception handling. Manual DI registration makes that worse because every extra behavior must be wired by hand.

DecoratR keeps the handler surface small.

1. Handlers contain business logic.
2. Decorators contain cross cutting behavior.
3. A source generator discovers everything during the build and validates the setup.
4. The host project receives a generated `AddDecoratR()` method that registers handlers and wraps them in the right order.

This means you get deterministic pipelines without a mediator dependency, without runtime assembly scanning, and without reflection based registration.

## Requirements and Package Model

DecoratR requires the .NET 10 SDK. Projects can target `net8.0`, `net9.0` or `net10.0`.

DecoratR is split into two packages.

1. [`DecoratR.Abstractions`](https://www.nuget.org/packages/DecoratR.Abstractions) contains the interfaces, attributes and options used in application code.
2. [`DecoratR.Generator`](https://www.nuget.org/packages/DecoratR.Generator) contains the source generator that emits registrations and cross assembly metadata.

Every project that declares handlers or decorators, and every composition root, installs both packages.

```bash
dotnet add package DecoratR.Abstractions
dotnet add package DecoratR.Generator
```

All projects of a solution must use the same DecoratR version. The metadata that flows between assemblies is defined by `DecoratR.Abstractions`, so mixing versions makes a composition root ignore libraries built with another version.

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

DecoratR discovers a handler when all of the following are true.

1. It is a non-abstract, non-static class or record (value types are reported with `DCTR010`).
2. It is not generic and not nested in a generic type.
3. It implements `IRequestHandler<TRequest, TResponse>` or `IStreamRequestHandler<TRequest, TResponse>`. A class that implements several handler interfaces is registered once per interface.
4. It is `public` or `internal` (nested types must be reachable from a top-level class in the same assembly), see `DCTR008`.

Only one handler per request/response pair is allowed. A second implementation is reported as `DCTR007`, because the DI container would silently resolve only the last registration.

Constructor injection works like any other DI based service.

### Decorators

Decorators wrap handlers and apply cross cutting behavior.

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

DecoratR discovers a class as a decorator when all of the following are true. Anything else is reported by the generator (`DCTR004` to `DCTR006`, `DCTR012`).

1. The class is marked with `[Decorator]` and is not abstract or static.
2. The class is open generic with exactly two type parameters. Their names and order do not matter; they must be used as the request and response type arguments of the handler interface.
3. The class implements exactly one of `IRequestHandler<TRequest, TResponse>` or `IStreamRequestHandler<TRequest, TResponse>`.
4. A public constructor accepts the inner handler (`IRequestHandler<TRequest, TResponse>`). Other parameters are resolved from the container.

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
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
```

`AddDecoratR()` is generated into the `Microsoft.Extensions.DependencyInjection` namespace, so no extra `using` is needed wherever `IServiceCollection` is available.

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

Every project that declares handlers or decorators references both packages and includes this attribute.

```csharp
using DecoratR;

[assembly: GenerateDecoratRMetadata]
```

That causes DecoratR to emit a handler registry, a decorator registry and assembly-level metadata for that assembly.

### 2. Mark the composition root with registration generation

The host project references both packages and includes this attribute.

```csharp
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

That host project receives `AddDecoratR()` and merges local and referenced metadata into one registration method. A project may carry both attributes.

### 3. Call `AddDecoratR()` in the host

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
```

### 4. Understand what crosses assembly boundaries

DecoratR propagates enough compile time metadata to let the host compose the final pipeline.

1. Request handlers discovered in library projects are exposed through a generated registry, so handlers can stay `internal`.
2. Decorators discovered in library projects are exposed through generated apply methods, so decorators can stay `internal`.
3. Request and response type hierarchies and decorator constraints are serialized so constrained decorators still match the right handlers in the host.

Request and response types must be `public` for decorators to be applied across assembly boundaries, because the host names them as type arguments. A library reports `DCTR013` for such handlers, and the host reports `DCTR011` when it has to skip decoration for them. The handler itself is still registered. Likewise, the constraint types of a library decorator must be public (`DCTR014`).

## Decorator Ordering

Ordering is deterministic.

1. Lower `Order` values are outermost.
2. Higher `Order` values are closer to the handler.
3. If two decorators share the same `Order`, DecoratR sorts them alphabetically by fully qualified type name. Local and referenced decorators are sorted together with the same rule.

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
public interface IQuery<TResult> : IRequest;

public sealed record CreateUserCommand(string Name) : ICommand;
public sealed record GetUsersQuery : IQuery<IReadOnlyList<User>>;

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

[Decorator(Order = 2)]
internal sealed class QueryCachingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IMemoryCache cache)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : class
{
    // ...
}
```

In that example, the first decorator applies to command handlers only, and the second one only to handlers whose request implements `IQuery<TResponse>` for the handler's own response type.

DecoratR matches constraints against the full request and response type hierarchy at build time.

1. Constraints on `TRequest` and on `TResponse` are both considered.
2. The type itself, its implemented interfaces and its base types are considered, so a constraint such as `ICommand` also applies when a request implements it indirectly.
3. Generic constraints that mention the other type parameter (`IQuery<TResponse>`) are matched with the handler's concrete types substituted.
4. The special constraints `class`, `struct`, `unmanaged` and `new()` are matched against the concrete types. `notnull` is not tracked and never excludes a handler.

A decorator whose constraints are not satisfied by a handler is simply not applied to it. The generated code only contains combinations that compile.

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
builder.Services.AddDecoratR(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
});
```

Important behavior:

1. The default lifetime is `Transient`.
2. Local handlers are registered directly in the generated host method; referenced handlers are registered through the generated registries of their assemblies.
3. Decorators inherit the lifetime of the registration they wrap.
4. Decoration replaces every registration of a service type, including registrations you added yourself before calling `AddDecoratR()` (factory and instance registrations included). Keyed registrations are left untouched.
5. Decorator and handler instances are created through `ActivatorUtilities`. Constructor selection happens once per registration when `AddDecoratR()` runs, not on every resolution.

## What DecoratR Generates

The generated output depends on which assembly attribute you use. With `EmitCompilerGeneratedFiles=true` the files can be inspected under `obj/`.

### `GenerateDecoratRMetadata`

Library projects receive generated metadata artifacts.

1. `DecoratRHandlerRegistry.g.cs`: a static `DecoratRHandlerRegistry` class with a `Handlers` array (request and stream handlers) plus `[DecoratRRegistry]` and `[DecoratRHandler]` assembly attributes.
2. `DecoratRDecoratorRegistry.g.cs`: a static `DecoratRDecoratorRegistry` class with one `Apply…<TRequest, TResponse>(ServiceDescriptor)` method per decorator plus `[DecoratRDecorator]` assembly attributes.

Both classes are placed in a namespace derived from the assembly name and hidden from IntelliSense. They are infrastructure for the composition root, not an API for application code.

### `GenerateDecoratRRegistrations`

Host projects receive `DecoratRServiceCollectionExtensions.g.cs` with `AddDecoratR()` in the `Microsoft.Extensions.DependencyInjection` namespace. It registers local and referenced handlers and applies one decorator pipeline per service type.

You do not need to manually register each handler or decorator.

## Diagnostics

DecoratR reports the following diagnostics. Errors stop the build; warnings point at setups that would otherwise fail at runtime or silently do nothing.

### DCTR001

Warning. The assembly is marked with `[GenerateDecoratRMetadata]` or `[GenerateDecoratRRegistrations]` but contains neither handlers nor decorators (and, for a composition root, references none).

### DCTR002

Hidden. Reports how many handlers were discovered. Visible with detailed build output.

### DCTR003

Hidden. Reports how many decorators were discovered.

### DCTR004

Error. A `[Decorator]` type does not implement exactly one of `IRequestHandler<,>` or `IStreamRequestHandler<,>`.

### DCTR005

Error. A `[Decorator]` type does not declare exactly two type parameters that are used as the request and response type arguments of its handler interface.

### DCTR006

Warning. A `[Decorator]` type is ignored because it is not generic, abstract, static, or nested in a generic type.

### DCTR007

Error. More than one handler implements the same `IRequestHandler<TRequest, TResponse>` (or stream equivalent). This includes a concrete base handler and a derived class, and collisions between local and referenced handlers.

### DCTR008

Warning. A handler or decorator is not accessible from generated code (for example a `private` nested class or a file-local type) and is skipped.

### DCTR009

Error. The composition root does not reference `Microsoft.Extensions.DependencyInjection.Abstractions`, so `AddDecoratR()` cannot be generated. Referencing `DecoratR.Abstractions` brings the dependency in.

### DCTR010

Warning. A handler is a value type and is skipped. Handlers must be classes or records.

### DCTR011

Warning. The composition root cannot apply decorators to a referenced service type because its request or response type is not public in the declaring assembly. The handler is still registered.

### DCTR012

Warning. A decorator has no public constructor with a parameter of the handler interface type. It is still applied, but resolving the pipeline will fail at runtime.

### DCTR013

Info. A library handler's request or response type is not public, so decorators from other assemblies will not be applied to it.

### DCTR014

Warning. A library decorator uses a constraint type that is not public (for example an `internal` marker interface). The generated apply method would be public and cannot name that type, so the decorator is not exported and is not applied by the composition root. Make the constraint type public, or declare the decorator in the composition root.

## Troubleshooting

### `AddDecoratR()` is missing

Check the following.

1. The host project references `DecoratR.Abstractions` and `DecoratR.Generator`.
2. The host project contains `[assembly: GenerateDecoratRRegistrations]`.
3. The build uses the .NET 10 SDK.
4. The build output does not contain `DCTR009`.

### A decorator is not applied

Check the following.

1. The build output does not contain `DCTR004`, `DCTR005`, `DCTR006` or `DCTR008` for the decorator.
2. The decorator implements the correct interface for the pipeline it targets.
3. The generic constraints on `TRequest` and `TResponse` match the handler's request and response types.
4. In a multi project setup, the assembly that defines the decorator is marked with `[assembly: GenerateDecoratRMetadata]`, and the request and response types of the handler are public (`DCTR011`, `DCTR013`).

### A handler is not discovered

Check the following.

1. The handler is a concrete, non-generic class or record.
2. The handler implements `IRequestHandler<TRequest, TResponse>` or `IStreamRequestHandler<TRequest, TResponse>`.
3. The project that owns the handler has the correct assembly attribute.
4. The build output does not contain `DCTR007`, `DCTR008` or `DCTR010` for the handler.

### Stream decorators do not affect regular handlers

This is expected. The pipelines are intentionally separate.

## Example Projects

1. Simple API example: [https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api](https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api)
2. Clean Architecture example: [https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture](https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture)
3. Modular Monolith example (two Clean Architecture modules, one API): [https://github.com/DecoratR/DecoratR/tree/main/examples/modular-monolith](https://github.com/DecoratR/DecoratR/tree/main/examples/modular-monolith)
4. Playground sample: [https://github.com/DecoratR/DecoratR/tree/main/playground](https://github.com/DecoratR/DecoratR/tree/main/playground)
