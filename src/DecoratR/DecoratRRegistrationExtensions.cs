using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

public static class DecoratRRegistrationExtensions
{
    private static readonly Type OpenHandlerInterface = typeof(IRequestHandler<,>);

    public static IServiceCollection AddDecoratR(
        this IServiceCollection services,
        Action<DecoratROptions> configure)
    {
        var options = new DecoratROptions();
        configure(options);

        foreach (var (serviceType, implementationType) in options.HandlerTypes)
        {
            services.Add(new ServiceDescriptor(serviceType, implementationType, ServiceLifetime.Transient));
        }

        ApplyDecorators(services, options.Decorators);

        return services;
    }

    private static void ApplyDecorators(
        IServiceCollection services, List<(Type DecoratorType, int Order)> decorators)
    {
        var handlerServiceTypes = services
            .Where(sd => sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == OpenHandlerInterface)
            .Select(sd => sd.ServiceType)
            .Distinct()
            .ToArray();

        // Stable sort ascending by Order, then reverse:
        // lowest Order = outermost (applied last, wraps everything)
        foreach (var (decoratorType, _) in decorators.OrderBy(d => d.Order).Reverse())
        {
            foreach (var handlerServiceType in handlerServiceTypes)
            {
                var closedDecorator = decoratorType.MakeGenericType(handlerServiceType.GetGenericArguments());
                services.Decorate(handlerServiceType, closedDecorator);
            }
        }
    }
}
