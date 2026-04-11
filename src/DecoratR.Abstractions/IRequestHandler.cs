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