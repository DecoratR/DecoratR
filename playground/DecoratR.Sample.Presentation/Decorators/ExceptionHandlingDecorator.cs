using System.Diagnostics;

namespace DecoratR.Sample.Presentation.Decorators;

internal sealed class ExceptionHandlingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IDecorator<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}