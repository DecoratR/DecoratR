using Examples.ModularMonolith.SharedKernel.Messaging;

namespace Examples.ModularMonolith.Orders.Contracts;

public sealed record OrderDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal Total);

public sealed record PlaceOrderCommand(Guid ProductId, int Quantity) : ICommand;

public sealed record GetOrderQuery(Guid Id) : IQuery<OrderDto?>;
