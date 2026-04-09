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
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}: {Payload}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        var response = await inner.HandleAsync(request, cancellationToken);
        stopwatch.Stop();

        logger.LogInformation("Handled {Request} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
