namespace DecoratR.Sample.Presentation.Decorators;

public class RequestLoggingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<RequestLoggingDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var requestType = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestType}: {@Request}", requestType, request);

        var response = await inner.HandleAsync(request, cancellationToken);

        logger.LogInformation("Handled {RequestType} → {@Response}", requestType, response);
        return response;
    }
}