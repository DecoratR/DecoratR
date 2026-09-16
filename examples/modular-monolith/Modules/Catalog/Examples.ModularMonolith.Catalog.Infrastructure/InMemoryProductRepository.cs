using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Examples.ModularMonolith.Catalog.Domain;

namespace Examples.ModularMonolith.Catalog.Infrastructure;

internal sealed class InMemoryProductRepository : IProductRepository
{
    public static readonly Guid KeyboardId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid MouseId = new("22222222-2222-2222-2222-222222222222");

    private readonly ConcurrentDictionary<Guid, Product> _products = new()
    {
        [KeyboardId] = Product.Restore(KeyboardId, "Keyboard", 49.90m),
        [MouseId] = Product.Restore(MouseId, "Mouse", 19.90m),
    };

    public ValueTask<Product?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_products.GetValueOrDefault(id));

    public ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Product>>(_products.Values.OrderBy(p => p.Name).ToList());

    public async IAsyncEnumerable<Product> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var product in _products.Values.OrderBy(p => p.Name))
        {
            await Task.Delay(50, cancellationToken);
            yield return product;
        }
    }

    public ValueTask AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products[product.Id] = product;
        return ValueTask.CompletedTask;
    }
}
