using System.Runtime.CompilerServices;
using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal sealed class StreamProductsQueryHandler(IProductRepository products)
    : IStreamRequestHandler<StreamProductsQuery, ProductDto>
{
    public async IAsyncEnumerable<ProductDto> HandleAsync(
        StreamProductsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var product in products.StreamAsync(cancellationToken))
            yield return product.ToDto();
    }
}
