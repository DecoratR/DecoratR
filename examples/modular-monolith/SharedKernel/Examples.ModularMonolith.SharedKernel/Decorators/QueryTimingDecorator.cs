using System.Diagnostics;
using DecoratR;
using Examples.ModularMonolith.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Examples.ModularMonolith.SharedKernel.Decorators;

/// <summary>
/// Applies only to queries: the constraint <c>IQuery&lt;TResponse&gt;</c> ties the request to the handler's
/// response type, so a handler returning a different type than its query declares is not decorated.
/// </summary>
[Decorator(Order = 1)]
internal sealed class QueryTimingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<QueryTimingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var start = Stopwatch.GetTimestamp();
        var response = await inner.HandleAsync(request, cancellationToken);
        logger.LogInformation("Query {Query} took {Elapsed}", typeof(TRequest).Name, Stopwatch.GetElapsedTime(start));
        return response;
    }
}
