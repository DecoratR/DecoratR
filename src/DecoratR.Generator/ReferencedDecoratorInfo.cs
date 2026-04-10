namespace DecoratR.Generator;

internal sealed class ReferencedDecoratorInfo(
    string applyMethodName,
    int order,
    EquatableArray<string> requestConstraintTypes) : IEquatable<ReferencedDecoratorInfo>
{
    public string ApplyMethodName { get; } = applyMethodName;

    public int Order { get; } = order;

    /// <summary>
    /// Fully qualified names of the type constraints on TRequest.
    /// Empty means the decorator applies to all request types.
    /// </summary>
    public EquatableArray<string> RequestConstraintTypes { get; } = requestConstraintTypes;

    public bool Equals(ReferencedDecoratorInfo? other)
    {
        if (other is null) return false;

        if (ReferenceEquals(this, other)) return true;

        return ApplyMethodName == other.ApplyMethodName &&
               Order == other.Order &&
               RequestConstraintTypes.Equals(other.RequestConstraintTypes);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ReferencedDecoratorInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ApplyMethodName.GetHashCode() * 397;
            hash ^= Order;
            hash = (hash * 397) ^ RequestConstraintTypes.GetHashCode();
            return hash;
        }
    }
}