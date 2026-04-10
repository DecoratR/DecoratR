using System.Diagnostics;
using DecoratR;
using Examples.CleanArchitecture.Application.Abstractions;

namespace Examples.CleanArchitecture.Api.Decorators;

[Decorator(Order = 2)]
internal sealed class CommandPerformanceLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<CommandPerformanceLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        var time = Stopwatch.GetElapsedTime(timestamp);

        logger.LogInformation(
            "Handled command {Request} in {ElapsedMs}ms",
            typeof(TRequest).Name,
            time.TotalMilliseconds);

        return response;
    }
}