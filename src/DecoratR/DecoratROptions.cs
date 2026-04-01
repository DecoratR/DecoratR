using System.Reflection;

namespace DecoratR;

public sealed class DecoratROptions
{
    internal HashSet<Assembly> Assemblies { get; } = [];

    internal List<DecoratorRegistration> Decorators { get; } = [];

    internal List<(Type ServiceType, Type ImplementationType)> HandlerTypes { get; } = [];

    /// <summary>
    /// Scan the given assembly for types implementing <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public DecoratROptions RegisterHandlersFromAssembly(params ReadOnlySpan<Assembly> assembly)
    {
        foreach (var a in assembly)
        {
            Assemblies.Add(a);
        }

        return this;
    }

    /// <summary>
    /// Scan the assembly containing <typeparamref name="T"/> for types implementing
    /// <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public DecoratROptions RegisterHandlersFromAssembly<T>()
    {
        Assemblies.Add(typeof(T).Assembly);
        return this;
    }

    /// <summary>
    /// Register handler types from a pre-built list of (ServiceType, ImplementationType) tuples.
    /// Intended for use with the DecoratR source generator's <c>HandlerRegistry.Handlers</c>.
    /// </summary>
    public DecoratROptions AddHandlers(List<(Type ServiceType, Type ImplementationType)> handlers)
    {
        HandlerTypes.AddRange(handlers);
        return this;
    }

    /// <summary>
    /// Add an open generic decorator type to the pipeline.
    /// Decorators are applied in registration order (first registered = outermost).
    /// </summary>
    public DecoratROptions AddDecorator(Type openGenericDecoratorType)
    {
        ValidateOpenGeneric(openGenericDecoratorType);
        Decorators.Add(new DecoratorRegistration(openGenericDecoratorType));
        return this;
    }

    /// <summary>
    /// Add an open generic decorator type to the pipeline, applying it only to handlers
    /// whose request type matches the given <paramref name="requestFilter"/>.
    /// </summary>
    public DecoratROptions AddDecorator(
        Type openGenericDecoratorType,
        Func<Type, bool> requestFilter)
    {
        ValidateOpenGeneric(openGenericDecoratorType);
        ArgumentNullException.ThrowIfNull(requestFilter);
        Decorators.Add(new DecoratorRegistration(openGenericDecoratorType, requestFilter));
        return this;
    }

    private static void ValidateOpenGeneric(Type openGenericDecoratorType)
    {
        if (!openGenericDecoratorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"Type {openGenericDecoratorType.Name} must be an open generic type definition.",
                nameof(openGenericDecoratorType));
        }

        var implementsHandler = openGenericDecoratorType
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

        if (!implementsHandler)
        {
            throw new ArgumentException(
                $"Type {openGenericDecoratorType.Name} must implement IRequestHandler<TRequest, TResponse>.",
                nameof(openGenericDecoratorType));
        }
    }

    internal sealed record DecoratorRegistration(Type DecoratorType, Func<Type, bool>? RequestFilter = null);
}