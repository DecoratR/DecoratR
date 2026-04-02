using System.Reflection;

namespace DecoratR;

public static class DecoratROptionsReflectionExtensions
{
    /// <summary>
    /// Scan the given assembly for types implementing <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
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
}
