# DecoratR

A lightweight decorator library for .NET that adds cross-cutting concerns to request handlers via the [Decorator Pattern](https://en.wikipedia.org/wiki/Decorator_pattern) — no mediator, no pipelines, no magic.

## Concept

DecoratR uses a Roslyn source generator to discover `IRequestHandler<TRequest, TResponse>` implementations at compile time and wraps them with any number of decorators. Handlers are resolved directly from the DI container by their interface — no mediator abstraction layer required.

```
Request → ExceptionHandling → Logging → Performance → Validation → Handler
```

Decorators are applied from outermost to innermost in registration order (first registered = outermost).

## Installation

```bash
dotnet add package DecoratR
```

For projects that only need the abstractions (e.g. application layer):

```bash
dotnet add package DecoratR.Abstractions
```

## Abstractions

### Requests

```csharp
// Base interface for all requests
public interface IRequest<TResponse>;

// Semantic markers for commands (write operations) and queries (read operations)
public interface ICommand<TResponse> : IRequest<TResponse>;
public interface IQuery<TResponse>   : IRequest<TResponse>;
```

### Handlers

```csharp
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
```

## Usage

### 1. Define a request

```csharp
public sealed record GreetCommand(string Name) : ICommand<string>;
```

### 2. Implement a handler

```csharp
internal sealed class GreetCommandHandler(IGreetingRepository repository)
    : IRequestHandler<GreetCommand, string>
{
    public async ValueTask<string> HandleAsync(GreetCommand command, CancellationToken cancellationToken = default)
    {
        var greeting = Greeting.Create(command.Name);
        await repository.AddAsync(greeting, cancellationToken);
        return greeting.Message;
    }
}
```

### 3. Write a decorator

A decorator implements the same `IRequestHandler<TRequest, TResponse>` interface and receives the inner handler via constructor injection:

```csharp
public class RequestLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<RequestLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {RequestType}: {@Request}", typeof(TRequest).Name, request);
        var response = await inner.HandleAsync(request, cancellationToken);
        logger.LogInformation("Handled {RequestType} → {@Response}", typeof(TRequest).Name, response);
        return response;
    }
}
```

### 4. Register

Add `[assembly: GenerateDecoratRRegistrations]` to the host project (e.g. in `AssemblyInfo.cs`). The source generator emits the `AddDecoratR()` extension method at compile time:

```csharp
builder.Services.AddDecoratR();
```

### 5. Resolve handlers

Handlers are injected directly via `IRequestHandler<TRequest, TResponse>` from the DI container — no mediator needed:

```csharp
app.MapPost("/greet", async (
    [FromServices] IRequestHandler<GreetCommand, string> handler,
    [FromBody] GreetRequest request,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(new GreetCommand(request.Name), cancellationToken);
    return Results.Ok(new { Message = result });
});
```

## Configuration Options

| Method | Description |
|---|---|
| `AddDecorator(Type)` | Registers an open-generic decorator for **all** handlers |
| `AddDecorator(Type, Func<Type, bool>)` | Registers a decorator with a filter on the request type |
| `AddCommandDecorator(Type)` | Decorator only for handlers of `ICommand<>` requests |
| `AddQueryDecorator(Type)` | Decorator only for handlers of `IQuery<>` requests |
| `WithLifetime(ServiceLifetime)` | Sets the `ServiceLifetime` for all handlers (default: `Transient`). Decorators automatically inherit the handler's lifetime. |

## Prerequisites

- .NET 10+
