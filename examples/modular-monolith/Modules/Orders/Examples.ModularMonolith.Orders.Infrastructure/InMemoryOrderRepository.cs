using System.Collections.Concurrent;
using Examples.ModularMonolith.Orders.Domain;

namespace Examples.ModularMonolith.Orders.Infrastructure;

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public ValueTask<Order?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_orders.GetValueOrDefault(id));

    public ValueTask AddAsync(Order order, CancellationToken cancellationToken)
    {
        _orders[order.Id] = order;
        return ValueTask.CompletedTask;
    }
}
