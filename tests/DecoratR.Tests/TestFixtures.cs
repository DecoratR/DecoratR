namespace DecoratR.Tests;

// Test request types
public sealed record TestCommand(string Value) : IRequest;

public sealed record TestQuery(int Id) : IRequest;

// Test handlers
public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
{
    public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult($"Handled: {request.Value}");
}

public sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
{
    public ValueTask<string> HandleAsync(TestQuery request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult($"Result: {request.Id}");
}

// Test decorator that tracks calls
public sealed class TrackingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public static int CallCount;

    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref CallCount);
        return await inner.HandleAsync(request, cancellationToken);
    }

    public static void Reset() => CallCount = 0;
}

// Second test decorator for ordering tests
public sealed class OuterDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(request, cancellationToken);
        return (TResponse) (object) $"Outer({result})";
    }
}

public sealed class InnerDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(request, cancellationToken);
        return (TResponse) (object) $"Inner({result})";
    }
}

// Decorator that throws
public sealed class ThrowingDecorator<TRequest, TResponse>
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Test exception");
}
