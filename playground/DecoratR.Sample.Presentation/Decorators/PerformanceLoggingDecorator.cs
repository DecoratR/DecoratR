using System.Diagnostics;

namespace DecoratR.Sample.Presentation.Decorators;

[Decorator(Order = 2)]
public class PerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<PerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(200);

    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(timestamp);

        if (elapsed > SlowThreshold)
        {
            logger.LogWarning("Slow request detected — {RequestType} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                typeof(TRequest).Name, elapsed.TotalMilliseconds, SlowThreshold.TotalMilliseconds);
        }
        else
        {
            logger.LogInformation("{RequestType} completed in {ElapsedMs}ms",
                typeof(TRequest).Name, elapsed.TotalMilliseconds);
        }

        return response;
    }
}