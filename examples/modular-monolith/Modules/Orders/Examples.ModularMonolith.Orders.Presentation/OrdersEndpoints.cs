using DecoratR;
using Examples.ModularMonolith.Orders.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Examples.ModularMonolith.Orders.Presentation;

public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (
            PlaceOrderCommand command,
            IRequestHandler<PlaceOrderCommand, OrderDto> handler,
            CancellationToken cancellationToken) =>
        {
            var order = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/orders/{order.Id}", order);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IRequestHandler<GetOrderQuery, OrderDto?> handler,
            CancellationToken cancellationToken) =>
            await handler.HandleAsync(new GetOrderQuery(id), cancellationToken) is { } order
                ? Results.Ok(order)
                : Results.NotFound());

        return endpoints;
    }
}
