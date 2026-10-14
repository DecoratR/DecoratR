namespace DecoratR.Runtime.Benchmarks;

/// <summary>Singleton dependency of every handler.</summary>
public sealed class Repository;

/// <summary>Singleton dependency of every decorator.</summary>
public sealed class Dependency;

public sealed record PingCommand : IRequest;

public sealed class PingHandler(Repository repository) : IRequestHandler<PingCommand, int>
{
    public Repository Repository { get; } = repository;

    public ValueTask<int> HandleAsync(PingCommand request, CancellationToken cancellationToken = default) => new(1);
}

[Decorator(Order = 0)]
public sealed class OuterDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner, Dependency dependency)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public Dependency Dependency { get; } = dependency;

    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) =>
        inner.HandleAsync(request, cancellationToken);
}

[Decorator(Order = 1)]
public sealed class MiddleDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner, Dependency dependency)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public Dependency Dependency { get; } = dependency;

    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) =>
        inner.HandleAsync(request, cancellationToken);
}

[Decorator(Order = 2)]
public sealed class InnerDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner, Dependency dependency)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public Dependency Dependency { get; } = dependency;

    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) =>
        inner.HandleAsync(request, cancellationToken);
}
