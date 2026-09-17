namespace DecoratR;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest" /> and returns <typeparamref name="TResponse" />.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest
{
    /// <summary>
    /// Asynchronously handles the specified <paramref name="request" /> and returns a response of type
    /// <typeparamref name="TResponse" />.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{TResult}" /> representing the asynchronous operation, containing the response.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a request of type <typeparamref name="TRequest" /> that produces no response.
/// </summary>
/// <remarks>
/// Implementations only provide <see cref="HandleAsync" />. The interface itself bridges to
/// <see cref="IRequestHandler{TRequest, TResponse}" /> with <see cref="Unit" /> as the response type, so every
/// decorator written against <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> applies to these handlers as well.
/// The generated registrations expose the decorated pipeline both as <c>IRequestHandler&lt;TRequest&gt;</c> and
/// as <c>IRequestHandler&lt;TRequest, Unit&gt;</c>.
/// </remarks>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    /// <summary>
    /// Asynchronously handles the specified <paramref name="request" />.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
    new ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default);

    ValueTask<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        // Completed handlers (the common case for in-memory work) do not go through a state machine at all;
        // only a pending ValueTask pays for the awaiting helper.
        var task = HandleAsync(request, cancellationToken);
        return task.IsCompletedSuccessfully ? new ValueTask<Unit>(Unit.Value) : Awaited(task);

        static async ValueTask<Unit> Awaited(ValueTask task)
        {
            await task.ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
