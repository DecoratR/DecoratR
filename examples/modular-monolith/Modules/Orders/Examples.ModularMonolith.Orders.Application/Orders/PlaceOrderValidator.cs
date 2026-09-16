using Examples.ModularMonolith.Orders.Contracts;
using Examples.ModularMonolith.SharedKernel.Validation;

namespace Examples.ModularMonolith.Orders.Application.Orders;

internal sealed class PlaceOrderValidator : IValidator<PlaceOrderCommand>
{
    public IReadOnlyList<string> Validate(PlaceOrderCommand request)
    {
        var errors = new List<string>();
        if (request.ProductId == Guid.Empty) errors.Add("ProductId must be set.");
        if (request.Quantity is < 1 or > 100) errors.Add("Quantity must be between 1 and 100.");
        return errors;
    }
}
