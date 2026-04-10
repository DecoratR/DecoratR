using System.Diagnostics;
using DecoratR;

[assembly: GenerateDecoratRMetadata]

namespace DecoratR.Sample.Infrastructure;

[Decorator(Order = 1)]
internal sealed class FooDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}