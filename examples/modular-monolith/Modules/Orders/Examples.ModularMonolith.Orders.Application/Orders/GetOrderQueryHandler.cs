using DecoratR;
using Examples.ModularMonolith.Orders.Contracts;
using Examples.ModularMonolith.Orders.Domain;

namespace Examples.ModularMonolith.Orders.Application.Orders;

internal sealed class GetOrderQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public async ValueTask<OrderDto?> HandleAsync(GetOrderQuery request, CancellationToken cancellationToken = default)
    {
        var order = await orders.GetAsync(request.Id, cancellationToken);
        return order?.ToDto();
    }
}
