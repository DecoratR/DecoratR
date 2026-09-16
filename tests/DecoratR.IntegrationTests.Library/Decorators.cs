using System.Runtime.CompilerServices;

namespace DecoratR.IntegrationTests.Library;

/// <summary>Applies to every request/response pipeline (Order 5).</summary>
[Decorator(Order = 5)]
internal sealed class LibraryLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add("LibraryLoggingDecorator:enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add("LibraryLoggingDecorator:exit");
        return response;
    }
}

/// <summary>Applies only to <see cref="ILibraryCommand"/> requests (Order 10, innermost).</summary>
[Decorator(Order = 10)]
internal sealed class LibraryCommandDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ILibraryCommand
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        trace.Add("LibraryCommandDecorator:enter");
        var response = await inner.HandleAsync(request, cancellationToken);
        trace.Add("LibraryCommandDecorator:exit");
        return response;
    }
}

/// <summary>Applies to every stream pipeline.</summary>
[Decorator(Order = 1)]
internal sealed class LibraryStreamDecorator<TRequest, TResponse>(
    IStreamRequestHandler<TRequest, TResponse> inner,
    ExecutionTrace trace)
    : IStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamRequest
{
    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        trace.Add("LibraryStreamDecorator:enter");
        await foreach (var item in inner.HandleAsync(request, cancellationToken))
            yield return item;
        trace.Add("LibraryStreamDecorator:exit");
    }
}
