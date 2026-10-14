using DecoratR.IntegrationTests.Library;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.IntegrationTests;

public sealed record HostCommand(string Name) : ILibraryCommand;

public sealed record HostQuery : IRequest;

public sealed class HostCommandHandler(ExecutionTrace trace) : IRequestHandler<HostCommand, string>
{
    public ValueTask<string> HandleAsync(HostCommand request, CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(HostCommandHandler));
        return ValueTask.FromResult($"Host: {request.Name}");
    }
}

/// <summary>A command without a response; also an <see cref="ILibraryCommand"/> so the constrained library decorator applies.</summary>
public sealed record HostVoidCommand(string Name) : ILibraryCommand;

public sealed class HostVoidCommandHandler(ExecutionTrace trace) : IRequestHandler<HostVoidCommand>
{
    public ValueTask HandleAsync(HostVoidCommand request, CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(HostVoidCommandHandler) + ":" + request.Name);
        return default;
    }
}

public sealed class HostQueryHandler(ExecutionTrace trace) : IRequestHandler<HostQuery, int>
{
    public ValueTask<int> HandleAsync(HostQuery request, CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(HostQueryHandler));
        return ValueTask.FromResult(7);
    }
}

public sealed record HostKeyedQuery : IRequest;

/// <summary>Takes a keyed service and the service provider; both are passed by the generated factory.</summary>
public sealed class HostKeyedQueryHandler([FromKeyedServices("greeting")] string greeting, IServiceProvider provider)
    : IRequestHandler<HostKeyedQuery, string>
{
    public ValueTask<string> HandleAsync(HostKeyedQuery request, CancellationToken cancellationToken = default)
    {
        provider.GetRequiredService<ExecutionTrace>().Add(nameof(HostKeyedQueryHandler));
        return ValueTask.FromResult(greeting);
    }
}

/// <summary>Outermost decorator for every request/response pipeline (Order 0).</summary>
[Decorator(Order = 0)]
public sealed class HostOuterDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add("HostOuterDecorator:enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add("HostOuterDecorator:exit");
        return response;
    }
}

/// <summary>
/// Shares Order 5 with <c>LibraryLoggingDecorator</c>; the tie is broken by the fully qualified type name, so this
/// decorator (DecoratR.IntegrationTests.HostTiedDecorator) runs outside of it (DecoratR.IntegrationTests.Library.LibraryLoggingDecorator).
/// </summary>
[Decorator(Order = 5)]
public sealed class HostTiedDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add("HostTiedDecorator:enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add("HostTiedDecorator:exit");
        return response;
    }
}

/// <summary>Applies only to pipelines with a value-type response (Order 7).</summary>
[Decorator(Order = 7)]
public sealed class HostStructResponseDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
    where TResponse : struct
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add("HostStructResponseDecorator:enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add("HostStructResponseDecorator:exit");
        return response;
    }
}

/// <summary>
/// Applies only to <see cref="ITaggedRequest"/> pipelines (Order 8). The optional parameter keeps the generator from
/// calling the constructor directly, so the decorator is created through ActivatorUtilities (<c>Wrap</c>).
/// </summary>
[Decorator(Order = 8)]
public sealed class HostFallbackDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace,
    string name = "HostFallbackDecorator")
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ITaggedRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add(name + ":enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add(name + ":exit");
        return response;
    }
}
