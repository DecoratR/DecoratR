using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.SharedKernel.Validation;

namespace Examples.ModularMonolith.Catalog.Application.Products;

internal sealed class CreateProductValidator : IValidator<CreateProductCommand>
{
    public IReadOnlyList<string> Validate(CreateProductCommand request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add("Name must not be empty.");
        if (request.Price <= 0) errors.Add("Price must be positive.");
        return errors;
    }
}
