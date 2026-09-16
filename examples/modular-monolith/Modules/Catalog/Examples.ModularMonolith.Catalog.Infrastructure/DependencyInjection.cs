using Examples.ModularMonolith.Catalog.Application;
using Examples.ModularMonolith.Catalog.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Examples.ModularMonolith.Catalog.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The single entry point the composition root uses to plug the Catalog module in.</summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddCatalogApplication();
        services.AddSingleton<IProductRepository, InMemoryProductRepository>();
        return services;
    }
}
