using DecoratR.Generator.Detection;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Model;

/// <summary>
/// Describes one handler service type (<c>IRequestHandler&lt;TRequest, TResponse&gt;</c> or the stream
/// equivalent) together with everything needed to match decorator constraints against it.
/// </summary>
internal sealed record ServiceTypeInfo(
    string RequestType,
    string ResponseType,
    bool IsStream,
    EquatableArray<string> RequestTypeHierarchy,
    EquatableArray<string> ResponseTypeHierarchy,
    bool IsPubliclyAccessible)
{
    /// <summary>The open handler interface, e.g. <c>global::DecoratR.IRequestHandler</c>.</summary>
    public string InterfaceName => WellKnownTypes.HandlerInterface(IsStream);

    /// <summary>The constructed service type, e.g. <c>global::DecoratR.IRequestHandler&lt;global::Cmd, string&gt;</c>.</summary>
    public string ConstructedInterface => InterfaceName + "<" + RequestType + ", " + ResponseType + ">";

    public static ServiceTypeInfo Create(INamedTypeSymbol handlerInterface, bool isStream, Compilation compilation)
    {
        var request = handlerInterface.TypeArguments[0];
        var response = handlerInterface.TypeArguments[1];

        return new ServiceTypeInfo(
            request.ToFullyQualifiedName(),
            response.ToFullyQualifiedName(),
            isStream,
            TypeHierarchy.Build(request, compilation),
            TypeHierarchy.Build(response, compilation),
            request.IsPubliclyAccessible() && response.IsPubliclyAccessible());
    }

    public static int Compare(ServiceTypeInfo a, ServiceTypeInfo b)
    {
        var cmp = a.IsStream.CompareTo(b.IsStream);
        if (cmp != 0) return cmp;

        cmp = string.CompareOrdinal(a.RequestType, b.RequestType);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.ResponseType, b.ResponseType);
    }

    /// <summary>Same runtime service type; nullable annotations do not distinguish DI registrations.</summary>
    public static bool SameService(ServiceTypeInfo a, ServiceTypeInfo b) =>
        a.IsStream == b.IsStream &&
        a.RequestType.StripNullableAnnotations() == b.RequestType.StripNullableAnnotations() &&
        a.ResponseType.StripNullableAnnotations() == b.ResponseType.StripNullableAnnotations();
}
