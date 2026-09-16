using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal static class ProductMapping
{
    public static ProductDto ToDto(this Product product) => new(product.Id, product.Name, product.Price);
}
