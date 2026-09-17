using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Detection;

internal static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat OpenGenericFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None);

    /// <summary>
    /// Fully qualified names keep nullable reference annotations (<c>IQuery&lt;ProductDto?&gt;</c>), so the
    /// emitted type arguments match the user's declarations exactly and do not trigger nullability warnings.
    /// </summary>
    public static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static string ToFullyQualifiedName(this ITypeSymbol type) => type.ToDisplayString(FullyQualifiedFormat);

    /// <summary>Removes nullable annotations; nullability is a compile-time concept the DI container does not know about.</summary>
    public static string StripNullableAnnotations(this string typeName) =>
        typeName.IndexOf('?') < 0 ? typeName : typeName.Replace("?", string.Empty);

    /// <summary>Fully qualified name without type arguments, e.g. <c>global::App.LoggingDecorator</c>.</summary>
    public static string ToOpenGenericName(this INamedTypeSymbol type) => type.ToDisplayString(OpenGenericFormat);

    public static bool IsDecoratRNamespace(this INamespaceSymbol? ns) =>
        ns is { Name: WellKnownTypes.RootNamespace, ContainingNamespace.IsGlobalNamespace: true };

    public static bool IsDecoratRMetadataNamespace(this INamespaceSymbol? ns) =>
        ns is { Name: WellKnownTypes.MetadataNamespace } && ns.ContainingNamespace.IsDecoratRNamespace();

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="iface"/> is <c>DecoratR.IRequestHandler&lt;,&gt;</c> or
    /// <c>DecoratR.IStreamRequestHandler&lt;,&gt;</c>.
    /// </summary>
    public static bool IsHandlerInterface(this INamedTypeSymbol iface, out bool isStream)
    {
        isStream = false;
        if (iface.TypeArguments.Length != 2) return false;

        var original = iface.OriginalDefinition;
        switch (original.MetadataName)
        {
            case WellKnownTypes.RequestHandlerMetadataName:
                break;
            case WellKnownTypes.StreamRequestHandlerMetadataName:
                isStream = true;
                break;
            default:
                return false;
        }

        return original.ContainingNamespace.IsDecoratRNamespace();
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="iface"/> is <c>DecoratR.IRequestHandler&lt;TRequest&gt;</c>,
    /// the handler interface for requests without a response.
    /// </summary>
    public static bool IsVoidHandlerInterface(this INamedTypeSymbol iface) =>
        iface.TypeArguments.Length == 1 &&
        iface.OriginalDefinition.MetadataName == WellKnownTypes.VoidRequestHandlerMetadataName &&
        iface.OriginalDefinition.ContainingNamespace.IsDecoratRNamespace();

    /// <summary>Returns <see langword="true"/> for <c>DecoratR.Unit</c>.</summary>
    public static bool IsUnit(this ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: WellKnownTypes.UnitName, IsGenericType: false } named &&
        named.ContainingNamespace.IsDecoratRNamespace();

    /// <summary>
    /// Whether <paramref name="type"/> implements <c>IRequestHandler&lt;TRequest&gt;</c> for the request type of
    /// <paramref name="handlerInterface"/>, i.e. the handler was declared without a response and the generated
    /// registrations must expose the pipeline as <c>IRequestHandler&lt;TRequest&gt;</c> as well.
    /// </summary>
    public static bool RegistersVoidFacade(this INamedTypeSymbol type, INamedTypeSymbol handlerInterface, bool isStream)
    {
        if (isStream || !handlerInterface.TypeArguments[1].IsUnit()) return false;

        foreach (var iface in type.AllInterfaces)
            if (iface.IsVoidHandlerInterface() &&
                SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], handlerInterface.TypeArguments[0]))
                return true;

        return false;
    }

    /// <summary>Returns <see langword="true"/> for <c>DecoratR.IRequest</c> and <c>DecoratR.IStreamRequest</c>.</summary>
    public static bool IsRequestMarkerInterface(this INamedTypeSymbol iface) =>
        iface.TypeArguments.Length == 0 &&
        iface.Name is WellKnownTypes.RequestMarkerName or WellKnownTypes.StreamRequestMarkerName &&
        iface.ContainingNamespace.IsDecoratRNamespace();

    public static bool HasDecoratorAttribute(this ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
            if (attribute.AttributeClass is { Name: WellKnownTypes.DecoratorAttributeName } attributeClass &&
                attributeClass.ContainingNamespace.IsDecoratRNamespace())
                return true;

        return false;
    }

    public static bool IsNestedInGenericType(this INamedTypeSymbol type)
    {
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
            if (containing.IsGenericType)
                return true;

        return false;
    }

    /// <summary>
    /// Whether generated code (a top-level static class in the same assembly) can reference the type:
    /// the type and all containing types must be public, internal or protected internal, and the type must not
    /// be file-local.
    /// </summary>
    public static bool IsAccessibleFromGeneratedCode(this INamedTypeSymbol type)
    {
        if (type.IsFileLocal) return false;

        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the type is public through all containing types and type arguments, so that code in another
    /// assembly can use it as a type argument.
    /// </summary>
    public static bool IsPubliclyAccessible(this ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return array.ElementType.IsPubliclyAccessible();

            case INamedTypeSymbol named:
                if (named.IsFileLocal) return false;

                if (named.IsTupleType)
                {
                    foreach (var element in named.TupleElements)
                        if (!element.Type.IsPubliclyAccessible())
                            return false;

                    return true;
                }

                for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
                {
                    if (current.DeclaredAccessibility != Accessibility.Public) return false;

                    foreach (var argument in current.TypeArguments)
                        if (!argument.IsPubliclyAccessible())
                            return false;
                }

                return true;

            default:
                // Type parameters, dynamic, pointers: nothing to check.
                return true;
        }
    }

    public static string ReadOrderArgument(this AttributeData attribute)
    {
        foreach (var named in attribute.NamedArguments)
            if (named.Key == "Order" && named.Value.Value is int order)
                return order.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return "0";
    }
}
