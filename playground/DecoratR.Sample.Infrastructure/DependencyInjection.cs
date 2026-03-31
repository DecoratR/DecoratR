using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Sample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingRepository, InMemoryGreetingRepository>();
        return services;
    }
}
