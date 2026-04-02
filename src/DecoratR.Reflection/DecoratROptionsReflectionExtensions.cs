using System.Reflection;

namespace DecoratR;

public static class DecoratROptionsReflectionExtensions
{
    /// <summary>
    /// Scan the given assemblies for types implementing <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public static DecoratROptions RegisterHandlersFromAssembly(
        this DecoratROptions options,
        params ReadOnlySpan<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            options.Assemblies.Add(assembly);
        }

        return options;
    }

    /// <summary>
    /// Scan the assembly containing <typeparamref name="T"/> for types implementing
    /// <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public static DecoratROptions RegisterHandlersFromAssembly<T>(this DecoratROptions options)
    {
        options.Assemblies.Add(typeof(T).Assembly);
        return options;
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
