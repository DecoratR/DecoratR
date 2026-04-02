namespace DecoratR.Generator;

internal sealed class DecoratorMetadata(string decoratorFullyQualifiedName) : IEquatable<DecoratorMetadata>
{
    public string DecoratorFullyQualifiedName { get; } = decoratorFullyQualifiedName;

    public bool Equals(DecoratorMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return DecoratorFullyQualifiedName == other.DecoratorFullyQualifiedName;
    }

    public override bool Equals(object? obj) => Equals(obj as DecoratorMetadata);

    public override int GetHashCode() => DecoratorFullyQualifiedName.GetHashCode();
}
