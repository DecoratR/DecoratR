using System.ComponentModel;

namespace DecoratR;

/// <summary>
/// Infrastructure type registered by the DecoratR source generator. Exposes the decorated
/// <see cref="IRequestHandler{TRequest, TResponse}" /> pipeline of a handler without a response as
/// <see cref="IRequestHandler{TRequest}" />, so callers can await a plain <see cref="ValueTask" />.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class VoidRequestHandler<TRequest>(IRequestHandler<TRequest, Unit> inner) : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        // Deliberately not an async method: an async wrapper would allocate a state machine box on every
        // asynchronous call. Completed results are returned without allocating; for pending results AsTask()
        // returns the Task that backs the decorated pipeline's ValueTask (async ValueTask<T> methods are
        // Task-backed), so this conversion is allocation-free in the common case.
        var task = inner.HandleAsync(request, cancellationToken);
        return task.IsCompletedSuccessfully ? default : new ValueTask(task.AsTask());
    }

    /// <summary>Forwards directly to the decorated pipeline without going through the <see cref="ValueTask" /> bridge.</summary>
    ValueTask<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(TRequest request, CancellationToken cancellationToken) =>
        inner.HandleAsync(request, cancellationToken);
}
