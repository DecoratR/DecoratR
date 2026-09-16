using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal sealed class ListProductsQueryHandler(IProductRepository products)
    : IRequestHandler<ListProductsQuery, IReadOnlyList<ProductDto>>
{
    public async ValueTask<IReadOnlyList<ProductDto>> HandleAsync(ListProductsQuery request, CancellationToken cancellationToken = default)
    {
        var all = await products.ListAsync(cancellationToken);
        return all.Select(p => p.ToDto()).ToList();
    }
}
