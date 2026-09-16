# DecoratR

[![NuGet (DecoratR.Abstractions)](https://img.shields.io/nuget/v/DecoratR.Abstractions.svg)](https://www.nuget.org/profiles/DecoratR)
[![License](https://img.shields.io/github/license/DecoratR/DecoratR.svg)](https://github.com/DecoratR/DecoratR/blob/main/LICENSE)

DecoratR builds ordered decorator pipelines for .NET request handlers at compile time.

You define requests, handlers, and open generic decorators. During the build, the source generator discovers them and emits a single `AddDecoratR()` extension method for `IServiceCollection`. The result is a focused handler model with compile time registration, deterministic decorator ordering, no runtime scanning, and no reflection driven DI wiring.

The full guide is available at [https://github.com/DecoratR/DecoratR/blob/main/docs/guide.md](https://github.com/DecoratR/DecoratR/blob/main/docs/guide.md).

## Why DecoratR

- Cross cutting behavior such as logging, validation, timing, retries, and exception handling moves out of handlers and into reusable decorators.
- Registration happens at build time, so startup stays simple and the generated code is easy to inspect.
- Decorators can target all requests or only specific request families through generic constraints on `TRequest` and `TResponse`.
- Request response handlers and stream handlers are both supported.
- Multi project solutions work without manual registration glue because metadata flows across assembly references.
- Misconfigurations (a decorator without a handler interface, two handlers for the same request, an inaccessible handler, …) are reported as compiler diagnostics instead of failing at runtime.
- The generated registration code is friendly to AOT and trimming scenarios.

## Requirements

- .NET 10 SDK (the generator targets the Roslyn version that ships with it). Projects can target `net8.0`, `net9.0` or `net10.0`.

## Packages

1. [`DecoratR.Abstractions`](https://www.nuget.org/packages/DecoratR.Abstractions) contains `IRequest`, `IRequestHandler<TRequest, TResponse>`, `IStreamRequest`, `IStreamRequestHandler<TRequest, TResponse>`, `DecoratorAttribute`, `DecoratROptions` and the assembly attributes that switch generation on.
2. [`DecoratR.Generator`](https://www.nuget.org/packages/DecoratR.Generator) contains the Roslyn source generator that emits registration code and cross assembly metadata.

## Install

Every project that takes part in DecoratR (handler libraries and the composition root alike) references both packages.

```bash
dotnet add package DecoratR.Abstractions
dotnet add package DecoratR.Generator
```

All projects of a solution must use the same DecoratR version, because the metadata that flows between assemblies is versioned with the packages.

## Quick Start

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

### 3. Add a decorator

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

### 4. Enable generation

```csharp
using DecoratR;

[assembly: GenerateDecoratRRegistrations]
```

### 5. Register DecoratR

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
```

`AddDecoratR()` is generated into the `Microsoft.Extensions.DependencyInjection` namespace, so it is available wherever `IServiceCollection` is. The injected `IRequestHandler<GetGreetingQuery, string>` is the fully decorated pipeline.

## What You Get

DecoratR generates `AddDecoratR()` for the host assembly and applies decorators in a deterministic order.

1. Lower `Order` values are outermost.
2. Higher `Order` values run closer to the handler.
3. When two decorators share the same `Order`, DecoratR sorts them alphabetically by fully qualified type name. This holds across assemblies.

For example, a pipeline with `Order = 1` exception handling and `Order = 2` validation runs like this:

```text
Request -> ExceptionHandlingDecorator -> ValidationDecorator -> Handler -> ValidationDecorator -> ExceptionHandlingDecorator -> Response
```

Regular and stream pipelines are isolated. A regular decorator never wraps a stream handler, and a stream decorator never wraps a regular handler.

## Configuration

Handlers are registered as `Transient` by default. You can override the lifetime for all generated registrations.

```csharp
builder.Services.AddDecoratR(options =>
{
    options.Lifetime = ServiceLifetime.Scoped;
});
```

Decorators inherit the lifetime of the handler they wrap.

## Example Projects

- Simple API example: [https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api](https://github.com/DecoratR/DecoratR/tree/main/examples/simple-api)
- Clean Architecture example: [https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture](https://github.com/DecoratR/DecoratR/tree/main/examples/clean-architecture)
- Modular Monolith example (two Clean Architecture modules composed by one API): [https://github.com/DecoratR/DecoratR/tree/main/examples/modular-monolith](https://github.com/DecoratR/DecoratR/tree/main/examples/modular-monolith)

## License

DecoratR is licensed under the MIT License. See [LICENSE](https://github.com/DecoratR/DecoratR/blob/main/LICENSE) for more information.
