namespace DecoratR;

/// <summary>
/// Handles a streaming request of type <typeparamref name="TRequest" /> and returns an
/// <see cref="IAsyncEnumerable{T}" /> of <typeparamref name="TResponse" />.
/// </summary>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest
{
    /// <summary>
    /// Asynchronously handles the specified <paramref name="request" /> and returns a stream of
    /// <typeparamref name="TResponse" /> items.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}" /> representing the asynchronous stream of responses.</returns>
    IAsyncEnumerable<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
