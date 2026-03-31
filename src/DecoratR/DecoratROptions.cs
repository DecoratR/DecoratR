using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

public sealed class DecoratROptions
{
    internal List<Assembly> Assemblies { get; } = [];
    internal List<DecoratorRegistration> Decorators { get; } = [];
    internal ServiceLifetime Lifetime { get; private set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Set the <see cref="ServiceLifetime"/> for all discovered handlers.
    /// Decorators automatically inherit the lifetime of the handler they wrap.
    /// Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public DecoratROptions WithLifetime(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Scan the given assembly for types implementing <see cref="IRequestHandler{TRequest,TResponse}"/> and register them.
    /// </summary>
    public DecoratROptions RegisterHandlersFromAssembly(params ReadOnlySpan<Assembly> assembly)
    {
        Assemblies.AddRange(assembly);
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

    /// <summary>
    /// Add an open generic decorator that applies only to command handlers
    /// (handlers for requests implementing <see cref="ICommand{TResponse}"/>).
    /// </summary>
    public DecoratROptions AddCommandDecorator(Type openGenericDecoratorType)
        => AddDecorator(openGenericDecoratorType, IsCommand);

    /// <summary>
    /// Add an open generic decorator that applies only to query handlers
    /// (handlers for requests implementing <see cref="IQuery{TResponse}"/>).
    /// </summary>
    public DecoratROptions AddQueryDecorator(Type openGenericDecoratorType)
        => AddDecorator(openGenericDecoratorType, IsQuery);

    private static void ValidateOpenGeneric(Type openGenericDecoratorType)
    {
        if (!openGenericDecoratorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"Type {openGenericDecoratorType.Name} must be an open generic type definition.",
                nameof(openGenericDecoratorType));
        } 
    }


    private static bool IsCommand(Type requestType)
        => requestType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static bool IsQuery(Type requestType)
        => requestType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    internal sealed record DecoratorRegistration(Type DecoratorType, Func<Type, bool>? RequestFilter = null);
}