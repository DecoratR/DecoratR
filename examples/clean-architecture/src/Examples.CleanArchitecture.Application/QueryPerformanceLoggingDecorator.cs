using System.Diagnostics;
using DecoratR;
using Examples.CleanArchitecture.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Examples.CleanArchitecture.Application;

[Decorator(Order = 2)]
internal sealed class QueryPerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<QueryPerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IQuery
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        var time = Stopwatch.GetElapsedTime(timestamp);

        logger.LogInformation(
            "Handled query {Request} in {ElapsedMs}ms",
            typeof(TRequest).Name,
            time.TotalMilliseconds);

        return response;
    }
}