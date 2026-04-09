using System.Diagnostics;
using DecoratR;

namespace Examples.CleanArchitecture.Api.Decorators;

[Decorator(Order = 2)]
internal sealed class PerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<PerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        var time = Stopwatch.GetElapsedTime(timestamp);

        logger.LogInformation(
            "Handled {Request} in {ElapsedMs}ms",
            typeof(TRequest).Name,
            time.TotalMilliseconds);

        return response;
    }
}