namespace DecoratR.Generator.Model;

/// <summary>
/// A discovered handler. <see cref="Location"/> is <see langword="null"/> for handlers read from referenced
/// assembly metadata.
/// </summary>
/// <param name="RegistersVoidFacade">
/// <see langword="true"/> when the handler implements <c>IRequestHandler&lt;TRequest&gt;</c> (no response). The
/// generated registrations then additionally expose the decorated <c>IRequestHandler&lt;TRequest, Unit&gt;</c>
/// pipeline as <c>IRequestHandler&lt;TRequest&gt;</c> through <c>VoidRequestHandler&lt;TRequest&gt;</c>.
/// Always <see langword="false"/> for referenced handlers: their facade is part of the library's registry.
/// </param>
internal sealed record HandlerMetadata(
    string HandlerType,
    ServiceTypeInfo ServiceType,
    LocationInfo? Location,
    bool RegistersVoidFacade = false)
{
    /// <summary>The facade service type, e.g. <c>global::DecoratR.IRequestHandler&lt;global::Cmd&gt;</c>.</summary>
    public string VoidFacadeInterface => WellKnownTypes.RequestHandler + "<" + ServiceType.RequestType + ">";

    /// <summary>The facade implementation type, e.g. <c>global::DecoratR.VoidRequestHandler&lt;global::Cmd&gt;</c>.</summary>
    public string VoidFacadeImplementation => WellKnownTypes.VoidRequestHandler + "<" + ServiceType.RequestType + ">";

    public static int Compare(HandlerMetadata a, HandlerMetadata b)
    {
        var cmp = ServiceTypeInfo.Compare(a.ServiceType, b.ServiceType);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.HandlerType, b.HandlerType);
    }
}
