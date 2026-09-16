namespace Examples.ModularMonolith.Catalog.Domain;

public interface IProductRepository
{
    ValueTask<Product?> GetAsync(Guid id, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<Product> StreamAsync(CancellationToken cancellationToken);

    ValueTask AddAsync(Product product, CancellationToken cancellationToken);
}
