using System.Diagnostics;
using DecoratR;

namespace Examples.CleanArchitecture.Api.Decorators;

[Decorator(Order = 1)]
internal sealed class ExceptionHandlingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<ExceptionHandlingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception handling {Request}", typeof(TRequest).Name);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}