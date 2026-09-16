using DecoratR.Generator.Detection;

namespace DecoratR.Generator.Model;

/// <summary>
/// A decorator discovered in the current compilation.
/// </summary>
/// <param name="DecoratorType">Open generic type name without type arguments, e.g. <c>global::App.LoggingDecorator</c>.</param>
/// <param name="TypeArgumentsTemplate">Type argument list with placeholders, e.g. <c>{TRequest}, {TResponse}</c>.</param>
/// <param name="RequestConstraints">Serialized constraints of the request type parameter.</param>
/// <param name="ResponseConstraints">Serialized constraints of the response type parameter.</param>
/// <param name="NonPublicConstraintType">
/// The first constraint type that is not public, or <see langword="null"/>. A decorator with such a constraint
/// cannot be exported through a public apply method (CS0703) and is skipped in the metadata path.
/// </param>
internal sealed record DecoratorMetadata(
    string DecoratorType,
    string TypeArgumentsTemplate,
    int Order,
    bool IsStream,
    EquatableArray<string> RequestConstraints,
    EquatableArray<string> ResponseConstraints,
    string? NonPublicConstraintType,
    LocationInfo? Location)
{
    /// <summary>Returns the constructed decorator type for the given request/response type names.</summary>
    public string Construct(string requestType, string responseType) =>
        DecoratorType + "<" + ConstraintSerializer.Substitute(TypeArgumentsTemplate, requestType, responseType) + ">";

    public static int Compare(DecoratorMetadata a, DecoratorMetadata b)
    {
        var cmp = a.Order.CompareTo(b.Order);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.DecoratorType, b.DecoratorType);
    }
}
