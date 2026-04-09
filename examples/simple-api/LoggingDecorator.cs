using System.Diagnostics;
using DecoratR;

namespace Examples.SimpleApi;

[Decorator]
internal sealed class LoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<LoggingDecorator<TRequest, TResponse>> logger)
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