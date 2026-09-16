using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal sealed class CreateProductCommandHandler(IProductRepository products)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async ValueTask<ProductDto> HandleAsync(CreateProductCommand request, CancellationToken cancellationToken = default)
    {
        var product = Product.Create(request.Name, request.Price);
        await products.AddAsync(product, cancellationToken);
        return product.ToDto();
    }
}
