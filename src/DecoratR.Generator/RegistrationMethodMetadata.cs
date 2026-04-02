namespace DecoratR.Generator;

internal sealed class RegistrationMethodMetadata(
    string fullyQualifiedClassName,
    string methodName) : IEquatable<RegistrationMethodMetadata>
{
    public string FullyQualifiedClassName { get; } = fullyQualifiedClassName;

    public string MethodName { get; } = methodName;

    public bool Equals(RegistrationMethodMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FullyQualifiedClassName == other.FullyQualifiedClassName
            && MethodName == other.MethodName;
    }

    public override bool Equals(object? obj) => Equals(obj as RegistrationMethodMetadata);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + FullyQualifiedClassName.GetHashCode();
            hash = hash * 31 + MethodName.GetHashCode();
            return hash;
        }
    }
}
