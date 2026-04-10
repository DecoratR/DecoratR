namespace DecoratR.Generator;

internal sealed class HandlerMetadata(
    string handlerFullyQualifiedName,
    string requestFullyQualifiedName,
    string responseFullyQualifiedName,
    EquatableArray<string> requestTypeHierarchy) : IEquatable<HandlerMetadata>
{
    public string HandlerFullyQualifiedName { get; } = handlerFullyQualifiedName;

    public string RequestFullyQualifiedName { get; } = requestFullyQualifiedName;

    public string ResponseFullyQualifiedName { get; } = responseFullyQualifiedName;

    /// <summary>
    /// All fully qualified type names in the request type's hierarchy
    /// (the type itself, all interfaces, and base types excluding System.Object).
    /// </summary>
    public EquatableArray<string> RequestTypeHierarchy { get; } = requestTypeHierarchy;

    public bool Equals(HandlerMetadata? other)
    {
        if (other is null) return false;

        if (ReferenceEquals(this, other)) return true;

        return HandlerFullyQualifiedName == other.HandlerFullyQualifiedName &&
               RequestFullyQualifiedName == other.RequestFullyQualifiedName &&
               ResponseFullyQualifiedName == other.ResponseFullyQualifiedName &&
               RequestTypeHierarchy.Equals(other.RequestTypeHierarchy);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HandlerMetadata);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + HandlerFullyQualifiedName.GetHashCode();
            hash = hash * 31 + RequestFullyQualifiedName.GetHashCode();
            hash = hash * 31 + ResponseFullyQualifiedName.GetHashCode();
            hash = hash * 31 + RequestTypeHierarchy.GetHashCode();
            return hash;
        }
    }
}