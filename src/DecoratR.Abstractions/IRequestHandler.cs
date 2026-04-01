namespace DecoratR;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> and returns <typeparamref name="TResponse"/>.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}