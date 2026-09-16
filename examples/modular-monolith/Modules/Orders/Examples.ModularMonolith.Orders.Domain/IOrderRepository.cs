namespace Examples.ModularMonolith.Orders.Domain;

public interface IOrderRepository
{
    ValueTask<Order?> GetAsync(Guid id, CancellationToken cancellationToken);

    ValueTask AddAsync(Order order, CancellationToken cancellationToken);
}
