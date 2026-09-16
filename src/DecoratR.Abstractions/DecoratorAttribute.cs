namespace DecoratR;

/// <summary>
/// Marks an open-generic class as a decorator in the DecoratR pipeline.
/// The source generator discovers decorators via this attribute.
/// </summary>
/// <remarks>
/// A decorator must be a non-abstract open generic class with exactly two type parameters that are used as
/// the <c>TRequest</c> and <c>TResponse</c> type arguments of the implemented
/// <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IStreamRequestHandler{TRequest, TResponse}"/>
/// interface. Constraints declared on either type parameter restrict the handlers the decorator is applied to.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DecoratorAttribute : Attribute
{
    /// <summary>
    /// Pipeline execution order. Lower values are applied further outside (they run first on the way in and
    /// last on the way out); higher values run closer to the handler.
    /// Decorators with the same order are sorted alphabetically by their fully qualified type name.
    /// </summary>
    public int Order { get; init; }
}
