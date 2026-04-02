using System.Reflection;
using System.Runtime.CompilerServices;

namespace DecoratR;

public static class DecoratROptionsReflectionExtensions
{
    private static readonly ConditionalWeakTable<DecoratROptions, HashSet<Assembly>> ScannedAssemblies = new();

    /// <summary>
    /// Scan the given assemblies for types implementing <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public static DecoratROptions RegisterHandlersFromAssembly(
        this DecoratROptions options,
        params ReadOnlySpan<Assembly> assemblies)
    {
        var scanned = ScannedAssemblies.GetOrCreateValue(options);

        foreach (var assembly in assemblies)
        {
            if (scanned.Add(assembly))
            {
                DiscoverHandlers(options, assembly);
            }
        }

        return options;
    }

    /// <summary>
    /// Scan the assembly containing <typeparamref name="T"/> for types implementing
    /// <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public static DecoratROptions RegisterHandlersFromAssembly<T>(this DecoratROptions options)
    {
        return options.RegisterHandlersFromAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Scan the given assemblies for open-generic types annotated with <see cref="DecoratorAttribute"/>
    /// and register them as decorators, ordered by <see cref="DecoratorAttribute.Order"/>.
    /// </summary>
    public static DecoratROptions RegisterDecoratorsFromAssembly(
        this DecoratROptions options,
        params ReadOnlySpan<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            DiscoverDecorators(options, assembly);
        }

        return options;
    }

    /// <summary>
    /// Scan the assembly containing <typeparamref name="T"/> for open-generic types annotated with
    /// <see cref="DecoratorAttribute"/> and register them as decorators.
    /// </summary>
    public static DecoratROptions RegisterDecoratorsFromAssembly<T>(this DecoratROptions options)
    {
        DiscoverDecorators(options, typeof(T).Assembly);
        return options;
    }

    private static void DiscoverHandlers(DecoratROptions options, Assembly assembly)
    {
        var handlerInterface = typeof(IRequestHandler<,>);
        var handlers = new List<(Type ServiceType, Type ImplementationType)>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != handlerInterface)
                    continue;

                handlers.Add((@interface, type));
                break;
            }
        }

        options.AddHandlers(handlers);
    }

    private static void DiscoverDecorators(DecoratROptions options, Assembly assembly)
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || !type.IsGenericTypeDefinition)
                continue;

            var attr = type.GetCustomAttribute<DecoratorAttribute>();
            if (attr is null)
                continue;

            var implementsHandler = type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface);

            if (!implementsHandler)
                continue;

            options.AddDecorator(type, attr.Order);
        }
    }
}
