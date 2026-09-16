using DecoratR;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.Orders.Contracts;
using Examples.ModularMonolith.Orders.Domain;
using Examples.ModularMonolith.SharedKernel.Validation;

namespace Examples.ModularMonolith.Orders.Application.Orders;

/// <summary>
/// Talks to the Catalog module through its public contract: the injected handler is Catalog's decorated
/// <see cref="GetProductQuery"/> pipeline, resolved from DI without a reference to Catalog's internals.
/// </summary>
internal sealed class PlaceOrderCommandHandler(
    IOrderRepository orders,
    IRequestHandler<GetProductQuery, ProductDto?> products)
    : IRequestHandler<PlaceOrderCommand, OrderDto>
{
    public async ValueTask<OrderDto> HandleAsync(PlaceOrderCommand request, CancellationToken cancellationToken = default)
    {
        var product = await products.HandleAsync(new GetProductQuery(request.ProductId), cancellationToken)
            ?? throw new ValidationException([$"Product '{request.ProductId}' does not exist."]);

        var order = Order.Place(product.Id, product.Name, request.Quantity, product.Price);
        await orders.AddAsync(order, cancellationToken);

        return order.ToDto();
    }
}
