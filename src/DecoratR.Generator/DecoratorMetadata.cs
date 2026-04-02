namespace DecoratR.Generator;

internal sealed class DecoratorMetadata(string decoratorFullyQualifiedName, int order) : IEquatable<DecoratorMetadata>
{
    public string DecoratorFullyQualifiedName { get; } = decoratorFullyQualifiedName;
    public int Order { get; } = order;

    public bool Equals(DecoratorMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return DecoratorFullyQualifiedName == other.DecoratorFullyQualifiedName
            && Order == other.Order;
    }

    public override bool Equals(object? obj) => Equals(obj as DecoratorMetadata);

    public override int GetHashCode()
    {
        unchecked
        {
            return (DecoratorFullyQualifiedName.GetHashCode() * 397) ^ Order;
        }
    }
}
