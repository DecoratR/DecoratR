namespace DecoratR.Generator;

internal sealed class HandlerMetadata(
    string handlerFullyQualifiedName,
    string requestFullyQualifiedName,
    string responseFullyQualifiedName) : IEquatable<HandlerMetadata>
{
    public string HandlerFullyQualifiedName { get; } = handlerFullyQualifiedName;

    public string RequestFullyQualifiedName { get; } = requestFullyQualifiedName;

    public string ResponseFullyQualifiedName { get; } = responseFullyQualifiedName;

    public bool Equals(HandlerMetadata? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return HandlerFullyQualifiedName == other.HandlerFullyQualifiedName && RequestFullyQualifiedName == other.RequestFullyQualifiedName && ResponseFullyQualifiedName == other.ResponseFullyQualifiedName;
    }

    public override bool Equals(object? obj) => Equals(obj as HandlerMetadata);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + HandlerFullyQualifiedName.GetHashCode();
            hash = hash * 31 + RequestFullyQualifiedName.GetHashCode();
            hash = hash * 31 + ResponseFullyQualifiedName.GetHashCode();
            return hash;
        }
    }
}
