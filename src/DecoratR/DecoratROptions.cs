using System.Diagnostics.CodeAnalysis;

namespace DecoratR;

public sealed class DecoratROptions
{
    internal List<(Type DecoratorType, int Order)> Decorators { get; } = [];

    internal List<(Type ServiceType, Type ImplementationType)> HandlerTypes { get; } = [];

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
    /// Lower <paramref name="order"/> values run first (outermost).
    /// </summary>
    public DecoratROptions AddDecorator([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type openGenericDecoratorType, int order = 0)
    {
        ValidateOpenGeneric(openGenericDecoratorType);
        Decorators.Add((openGenericDecoratorType, order));
        return this;
    }

    private static void ValidateOpenGeneric([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type openGenericDecoratorType)
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
}
