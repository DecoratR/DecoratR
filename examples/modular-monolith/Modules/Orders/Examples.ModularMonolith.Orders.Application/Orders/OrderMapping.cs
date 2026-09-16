using Examples.ModularMonolith.Orders.Contracts;
using Examples.ModularMonolith.Orders.Domain;

namespace Examples.ModularMonolith.Orders.Application.Orders;

internal static class OrderMapping
{
    public static OrderDto ToDto(this Order order) =>
        new(order.Id, order.ProductId, order.ProductName, order.Quantity, order.Total);
}
