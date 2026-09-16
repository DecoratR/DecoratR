using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal sealed class GetProductQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductQuery, ProductDto?>
{
    public async ValueTask<ProductDto?> HandleAsync(GetProductQuery request, CancellationToken cancellationToken = default)
    {
        var product = await products.GetAsync(request.Id, cancellationToken);
        return product?.ToDto();
    }
}
