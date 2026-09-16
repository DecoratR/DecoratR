using Examples.ModularMonolith.Orders.Application;
using Examples.ModularMonolith.Orders.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Examples.ModularMonolith.Orders.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The single entry point the composition root uses to plug the Orders module in.</summary>
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddOrdersApplication();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        return services;
    }
}
