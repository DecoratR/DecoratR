namespace DecoratR;

/// <summary>
/// Marker interface identifying an open-generic decorator in the pipeline.
/// Decorators implement this interface instead of <see cref="IRequestHandler{TRequest,TResponse}"/>
/// directly so the DecoratR source generator can distinguish them from handlers.
/// </summary>
public interface IDecorator<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest;
