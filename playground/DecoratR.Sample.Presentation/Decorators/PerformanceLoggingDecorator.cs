using System.Diagnostics;

namespace DecoratR.Sample.Presentation.Decorators;

[Decorator(Order = 2)]
public class PerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<PerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(timestamp);

        logger.LogInformation("{RequestType} completed in {ElapsedMs}ms", typeof(TRequest).Name,
            elapsed.TotalMilliseconds);

        return response;
    }
}