namespace DecoratR.Generator;

internal sealed class DecoratorMetadata(
    string decoratorFullyQualifiedName,
    int order,
    EquatableArray<string> requestConstraintTypes,
    bool isStream = false) : IEquatable<DecoratorMetadata>
{
    public string DecoratorFullyQualifiedName { get; } = decoratorFullyQualifiedName;

    public int Order { get; } = order;

    /// <summary>
    /// Fully qualified names of the type constraints on TRequest.
    /// E.g. ["global::DecoratR.IRequest"] for unconstrained, ["global::MyApp.ICommand"] for constrained.
    /// </summary>
    public EquatableArray<string> RequestConstraintTypes { get; } = requestConstraintTypes;

    /// <summary>
    /// Whether this decorator targets <c>IStreamRequestHandler</c> instead of <c>IRequestHandler</c>.
    /// </summary>
    public bool IsStream { get; } = isStream;

    public bool Equals(DecoratorMetadata? other)
    {
        if (other is null) return false;

        if (ReferenceEquals(this, other)) return true;

        return DecoratorFullyQualifiedName == other.DecoratorFullyQualifiedName &&
               Order == other.Order &&
               IsStream == other.IsStream &&
               RequestConstraintTypes.Equals(other.RequestConstraintTypes);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DecoratorMetadata);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = DecoratorFullyQualifiedName.GetHashCode() * 397;
            hash ^= Order;
            hash = (hash * 397) ^ IsStream.GetHashCode();
            hash = (hash * 397) ^ RequestConstraintTypes.GetHashCode();
            return hash;
        }
    }
}