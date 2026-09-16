using DecoratR;
using Microsoft.Extensions.Logging;

namespace Examples.ModularMonolith.SharedKernel.Decorators;

/// <summary>Outermost decorator of every request/response pipeline in every module.</summary>
[Decorator(Order = 0)]
internal sealed class LoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<LoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        try
        {
            var response = await inner.HandleAsync(request, cancellationToken);
            logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}
