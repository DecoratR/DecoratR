using System.Reflection;
using MsDi = Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

public static class DecoratRRegistrationExtensions
{
    private static readonly Type OpenHandlerInterface = typeof(IRequestHandler<,>);

    public static MsDi.IServiceCollection AddDecoratR(
        this MsDi.IServiceCollection services,
        Action<DecoratROptions> configure)
    {
        var options = new DecoratROptions();
        configure(options);

        var msLifetime = MapLifetime(options.Lifetime);

        if (options.Assemblies.Count == 0 && options.HandlerTypes.Count == 0)
        {
            options.RegisterHandlersFromAssembly(Assembly.GetExecutingAssembly());
        }

        foreach (var assembly in options.Assemblies)
        {
            RegisterHandlersFromAssembly(services, assembly, msLifetime);
        }

        foreach (var (serviceType, implementationType) in options.HandlerTypes)
        {
            services.Add(new MsDi.ServiceDescriptor(serviceType, implementationType, msLifetime));
        }

        ApplyDecorators(services, options.Decorators);

        return services;
    }

    private static void RegisterHandlersFromAssembly(
        MsDi.IServiceCollection services, Assembly assembly, MsDi.ServiceLifetime lifetime)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != OpenHandlerInterface)
                {
                    continue;
                }

                services.Add(new MsDi.ServiceDescriptor(@interface, type, lifetime));
                break;
            }
        }
    }

    private static void ApplyDecorators(MsDi.IServiceCollection services, List<DecoratROptions.DecoratorRegistration> decorators)
    {
        var handlerServiceTypes = services
            .Where(sd => sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == OpenHandlerInterface)
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
                {
                    continue;
                }

                var closedDecorator = registration.DecoratorType.MakeGenericType(genericArgs);
                services.Decorate(handlerServiceType, closedDecorator);
            }
        }
    }

    private static MsDi.ServiceLifetime MapLifetime(ServiceLifetime lifetime) => lifetime switch
    {
        ServiceLifetime.Transient => MsDi.ServiceLifetime.Transient,
        ServiceLifetime.Scoped => MsDi.ServiceLifetime.Scoped,
        ServiceLifetime.Singleton => MsDi.ServiceLifetime.Singleton,
        _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
    };
}