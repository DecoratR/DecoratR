using Examples.ModularMonolith.Orders.Application.Orders;
using Examples.ModularMonolith.Orders.Contracts;
using Examples.ModularMonolith.SharedKernel.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Examples.ModularMonolith.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<PlaceOrderCommand>, PlaceOrderValidator>();
        return services;
    }
}
