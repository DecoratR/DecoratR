using Examples.ModularMonolith.Catalog.Application.Products;
using Examples.ModularMonolith.Catalog.Contracts;
using Examples.ModularMonolith.SharedKernel.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Examples.ModularMonolith.Catalog.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application services of the module. Handlers and decorators are not registered here;
    /// DecoratR wires them in the composition root through the metadata this assembly exports.
    /// </summary>
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateProductCommand>, CreateProductValidator>();
        return services;
    }
}
