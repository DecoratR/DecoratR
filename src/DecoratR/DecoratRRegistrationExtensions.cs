using System.Reflection;
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

        if (options.Assemblies.Count == 0)
        {
            options.Assemblies.Add(Assembly.GetExecutingAssembly());
        }

        foreach (var assembly in options.Assemblies)
        {
            RegisterHandlersFromAssembly(services, assembly, options.Lifetime);
        }

        ApplyDecorators(services, options.Decorators);

        return services;
    }
    
    private static void RegisterHandlersFromAssembly(
        IServiceCollection services, Assembly assembly, ServiceLifetime lifetime)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != OpenHandlerInterface)
                    continue;

                services.Add(new ServiceDescriptor(@interface, type, lifetime));
                break;
            }
        }
    }
    
    private static void ApplyDecorators(IServiceCollection services, List<DecoratROptions.DecoratorRegistration> decorators)
    {
        var handlerServiceTypes = services
            .Where(sd => sd.ServiceType.IsGenericType
                         && sd.ServiceType.GetGenericTypeDefinition() == OpenHandlerInterface)
            .Select(sd => sd.ServiceType)
            .Distinct()
            .ToArray();

        // Apply in reverse so first registered = outermost
        foreach (var registration in Enumerable.Reverse(decorators))
        {
            foreach (var handlerServiceType in handlerServiceTypes)
            {
                var genericArgs = handlerServiceType.GetGenericArguments();
                var requestType = genericArgs[0];

                if (registration.RequestFilter is not null && !registration.RequestFilter(requestType))
                    continue;

                var closedDecorator = registration.DecoratorType.MakeGenericType(genericArgs);
                services.Decorate(handlerServiceType, closedDecorator);
            }
        }
    }
}
