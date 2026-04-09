using System.Diagnostics;
using DecoratR;

namespace Examples.CleanArchitecture.Api.Decorators;

[Decorator(Order = 2)]
public sealed class PerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<PerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await inner.HandleAsync(request, cancellationToken);
        stopwatch.Stop();

        logger.LogInformation(
            "Handled {Request} in {ElapsedMs}ms",
            typeof(TRequest).Name,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
