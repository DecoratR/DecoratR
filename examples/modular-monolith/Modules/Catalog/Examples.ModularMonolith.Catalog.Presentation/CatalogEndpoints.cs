using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Examples.ModularMonolith.Catalog.Presentation;

public static class CatalogEndpoints
{
    public sealed record CreateProductRequest(string Name, decimal Price);

    /// <summary>
    /// Endpoints only depend on the module's contracts and resolve the decorated handler pipelines from DI.
    /// </summary>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog/products").WithTags("Catalog");

        group.MapGet("/", async (
            IRequestHandler<ListProductsQuery, IReadOnlyList<ProductDto>> handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new ListProductsQuery(), cancellationToken)));

        group.MapGet("/stream", (
            IStreamRequestHandler<StreamProductsQuery, ProductDto> handler,
            CancellationToken cancellationToken) =>
            handler.HandleAsync(new StreamProductsQuery(), cancellationToken));

        group.MapGet("/{id:guid}", async (
            Guid id,
            IRequestHandler<GetProductQuery, ProductDto?> handler,
            CancellationToken cancellationToken) =>
            await handler.HandleAsync(new GetProductQuery(id), cancellationToken) is { } product
                ? Results.Ok(product)
                : Results.NotFound());

        group.MapPost("/", async (
            CreateProductRequest body,
            IRequestHandler<CreateProductCommand, ProductDto> handler,
            CancellationToken cancellationToken) =>
        {
            var product = await handler.HandleAsync(new CreateProductCommand(body.Name, body.Price), cancellationToken);
            return Results.Created($"/catalog/products/{product.Id}", product);
        });

        return endpoints;
    }
}
