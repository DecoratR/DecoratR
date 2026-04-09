namespace DecoratR.Generator;

internal sealed class ReferencedDecoratorInfo(string applyMethodName, int order) : IEquatable<ReferencedDecoratorInfo>
{
    public string ApplyMethodName { get; } = applyMethodName;

    public int Order { get; } = order;

    public bool Equals(ReferencedDecoratorInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ApplyMethodName == other.ApplyMethodName && Order == other.Order;
    }

    public override bool Equals(object? obj) => Equals(obj as ReferencedDecoratorInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            return ApplyMethodName.GetHashCode() * 397 ^ Order;
        }
    }
}