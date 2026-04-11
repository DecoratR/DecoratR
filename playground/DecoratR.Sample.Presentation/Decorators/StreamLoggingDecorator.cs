using System.Runtime.CompilerServices;
using DecoratR.Sample.Application.Abstractions;

namespace DecoratR.Sample.Presentation.Decorators;

[Decorator(Order = 1)]
public class StreamLoggingDecorator<TRequest, TResponse>(
    IStreamRequestHandler<TRequest, TResponse> inner,
    ILogger<StreamLoggingDecorator<TRequest, TResponse>> logger)
    : IStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamQuery
{
    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestType = typeof(TRequest).Name;
        logger.LogInformation("Starting stream for {RequestType}: {@Request}", requestType, request);

        var count = 0;
        await foreach (var item in inner.HandleAsync(request, cancellationToken))
        {
            count++;
            yield return item;
        }

        logger.LogInformation("Completed stream for {RequestType}, yielded {Count} items", requestType, count);
    }
}
