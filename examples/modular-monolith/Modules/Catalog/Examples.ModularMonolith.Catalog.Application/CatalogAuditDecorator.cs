using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Microsoft.Extensions.Logging;

namespace Examples.ModularMonolith.Catalog.Application;

/// <summary>
/// A module-scoped decorator: it is constrained to <see cref="ICatalogCommand"/>, so it wraps Catalog commands
/// only, no matter which module dispatches them. It runs inside the shared logging decorator (Order 0) and
/// outside validation (Order 10).
/// </summary>
[Decorator(Order = 5)]
internal sealed class CatalogAuditDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    ILogger<CatalogAuditDecorator<TRequest, TResponse>> logger)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICatalogCommand
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var response = await inner.HandleAsync(request, cancellationToken);
        logger.LogInformation("Catalog audit: {Command} => {Response}", request, response);
        return response;
    }
}
