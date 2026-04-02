namespace DecoratR;

/// <summary>
/// Marks an open-generic class as a decorator in the DecoratR pipeline.
/// The source generator and reflection-based registration discover decorators via this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DecoratorAttribute : Attribute
{
    /// <summary>
    /// Pipeline execution order. Lower values run first (outermost).
    /// Decorators with the same order are applied in discovery order.
    /// </summary>
    public int Order { get; init; }
}
