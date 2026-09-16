namespace DecoratR.Generator.Model;

/// <summary>
/// A discovered handler. <see cref="Location"/> is <see langword="null"/> for handlers read from referenced
/// assembly metadata.
/// </summary>
internal sealed record HandlerMetadata(string HandlerType, ServiceTypeInfo ServiceType, LocationInfo? Location)
{
    public static int Compare(HandlerMetadata a, HandlerMetadata b)
    {
        var cmp = ServiceTypeInfo.Compare(a.ServiceType, b.ServiceType);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.HandlerType, b.HandlerType);
    }
}
