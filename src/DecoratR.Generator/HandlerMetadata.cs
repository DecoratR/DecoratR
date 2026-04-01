namespace DecoratR.Generator;

internal enum RequestCategory
{
    Request,
    Command,
    Query
}

internal sealed class HandlerMetadata : IEquatable<HandlerMetadata>
{
    public HandlerMetadata(
        string handlerFullyQualifiedName,
        string requestFullyQualifiedName,
        string responseFullyQualifiedName,
        RequestCategory category)
    {
        HandlerFullyQualifiedName = handlerFullyQualifiedName;
        RequestFullyQualifiedName = requestFullyQualifiedName;
        ResponseFullyQualifiedName = responseFullyQualifiedName;
        Category = category;
    }

    public string HandlerFullyQualifiedName { get; }

    public string RequestFullyQualifiedName { get; }

    public string ResponseFullyQualifiedName { get; }

    public RequestCategory Category { get; }

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

        return HandlerFullyQualifiedName == other.HandlerFullyQualifiedName && RequestFullyQualifiedName == other.RequestFullyQualifiedName && ResponseFullyQualifiedName == other.ResponseFullyQualifiedName && Category == other.Category;
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
            hash = hash * 31 + (int) Category;
            return hash;
        }
    }
}