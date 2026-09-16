using System.ComponentModel;

namespace DecoratR.Metadata;

/// <summary>
/// Infrastructure attribute emitted by the DecoratR source generator. Describes one decorator of an assembly and
/// the generated apply method a composition root calls to wrap a service descriptor with it, without referencing
/// the (possibly internal) decorator type directly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DecoratRDecoratorAttribute(string applyMethod, string decoratorType, int order, bool isStream)
    : Attribute
{
    /// <summary>
    /// The fully qualified (<c>global::</c>-prefixed) name of the generated generic apply method.
    /// </summary>
    public string ApplyMethod { get; } = applyMethod;

    /// <summary>
    /// The fully qualified name of the open generic decorator type without type arguments.
    /// Used as the secondary sort key when decorators share the same <see cref="Order"/>.
    /// </summary>
    public string DecoratorType { get; } = decoratorType;

    /// <summary>The pipeline order declared via <see cref="DecoratorAttribute.Order"/>.</summary>
    public int Order { get; } = order;

    /// <summary>
    /// <see langword="true"/> for stream decorators, <see langword="false"/> for request/response decorators.
    /// </summary>
    public bool IsStream { get; } = isStream;

    /// <summary>
    /// Semicolon-delimited constraints of the request type parameter. Type constraints are fully qualified names in
    /// which the decorator's own type parameters are replaced by <c>{TRequest}</c> and <c>{TResponse}</c>; special
    /// constraints are encoded as <c>!class</c>, <c>!struct</c>, <c>!notnull</c>, <c>!unmanaged</c> and <c>!new</c>.
    /// </summary>
    public string RequestConstraints { get; init; } = "";

    /// <summary>
    /// Semicolon-delimited constraints of the response type parameter, encoded like <see cref="RequestConstraints"/>.
    /// </summary>
    public string ResponseConstraints { get; init; } = "";
}
