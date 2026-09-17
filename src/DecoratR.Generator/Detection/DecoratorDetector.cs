using System.Collections.Immutable;
using DecoratR.Generator.Model;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Validates <c>[Decorator]</c> types and extracts their metadata.
/// </summary>
internal static class DecoratorDetector
{
    public static DecoratorDetectionResult Detect(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
            return new DecoratorDetectionResult(null, EquatableArray<DiagnosticInfo>.Empty);

        var location = LocationInfo.From(symbol);
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        var ignoreReason = GetIgnoreReason(symbol);
        if (ignoreReason is not null)
            return Failure(Diagnostics.DecoratorIgnored, location, displayName, ignoreReason);

        if (!symbol.IsAccessibleFromGeneratedCode())
            return Failure(Diagnostics.TypeNotAccessible, location, displayName);

        INamedTypeSymbol? handlerInterface = null;
        var isStream = false;
        var handlerInterfaceCount = 0;
        foreach (var iface in symbol.AllInterfaces)
        {
            if (!iface.IsHandlerInterface(out var stream)) continue;

            handlerInterfaceCount++;
            handlerInterface = iface;
            isStream = stream;
        }

        if (handlerInterface is null || handlerInterfaceCount != 1)
            return Failure(Diagnostics.DecoratorMissingHandlerInterface, location, displayName,
                handlerInterfaceCount == 0 ? "none" : $"{handlerInterfaceCount} handler interfaces");

        if (symbol.TypeParameters.Length != 2 ||
            handlerInterface.TypeArguments[0] is not ITypeParameterSymbol requestParameter ||
            handlerInterface.TypeArguments[1] is not ITypeParameterSymbol responseParameter ||
            requestParameter.Ordinal == responseParameter.Ordinal ||
            !SymbolEqualityComparer.Default.Equals(requestParameter.ContainingSymbol, symbol) ||
            !SymbolEqualityComparer.Default.Equals(responseParameter.ContainingSymbol, symbol))
            return Failure(Diagnostics.DecoratorTypeParameterMismatch, location, displayName, VoidDecoratorHint(symbol));

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        if (!HasInnerHandlerConstructor(symbol, handlerInterface))
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.DecoratorMissingInnerConstructor, location, displayName,
                handlerInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

        var template = requestParameter.Ordinal == 0
            ? ConstraintSerializer.RequestPlaceholder + ", " + ConstraintSerializer.ResponsePlaceholder
            : ConstraintSerializer.ResponsePlaceholder + ", " + ConstraintSerializer.RequestPlaceholder;

        var order = 0;
        foreach (var attribute in context.Attributes)
            order = int.Parse(attribute.ReadOrderArgument(), System.Globalization.CultureInfo.InvariantCulture);

        var decorator = new DecoratorMetadata(
            symbol.ToOpenGenericName(),
            template,
            order,
            isStream,
            ConstraintSerializer.Serialize(requestParameter, requestParameter, responseParameter),
            ConstraintSerializer.Serialize(responseParameter, requestParameter, responseParameter),
            FindNonPublicConstraintType(requestParameter) ?? FindNonPublicConstraintType(responseParameter),
            location);

        return new DecoratorDetectionResult(decorator, diagnostics.ToImmutable());
    }

    public static DecoratorSet Aggregate(ImmutableArray<DecoratorDetectionResult> results)
    {
        var decorators = new List<DecoratorMetadata>(results.Length);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        foreach (var result in results)
        {
            if (result.Decorator is not null) decorators.Add(result.Decorator);
            diagnostics.AddRange(result.Diagnostics);
        }

        decorators.Sort(DecoratorMetadata.Compare);

        return new DecoratorSet(decorators.ToImmutableArray(), diagnostics.ToImmutable());
    }

    private static string? GetIgnoreReason(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class) return "is not a class";
        if (symbol.IsAbstract) return "is abstract";
        if (symbol.IsStatic) return "is static";
        if (!symbol.IsGenericType) return "is not an open generic type; a decorator needs type parameters for TRequest and TResponse";
        if (symbol.IsNestedInGenericType()) return "is nested in a generic type";
        return null;
    }

    /// <summary>
    /// A decorator written against <c>IRequestHandler&lt;TRequest&gt;</c> implicitly implements
    /// <c>IRequestHandler&lt;TRequest, Unit&gt;</c>, which fails the type parameter check. Point the author to
    /// the two-parameter form, which covers handlers without a response as well.
    /// </summary>
    private static string VoidDecoratorHint(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
            if (iface.IsVoidHandlerInterface())
                return "; decorators for handlers without a response must implement IRequestHandler<TRequest, TResponse> too (TResponse is Unit for those pipelines)";

        return string.Empty;
    }

    private static string? FindNonPublicConstraintType(ITypeParameterSymbol typeParameter)
    {
        foreach (var constraint in typeParameter.ConstraintTypes)
            if (!constraint.IsPubliclyAccessible())
                return constraint.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        return null;
    }

    private static bool HasInnerHandlerConstructor(INamedTypeSymbol symbol, INamedTypeSymbol handlerInterface)
    {
        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public) continue;

            foreach (var parameter in constructor.Parameters)
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, handlerInterface))
                    return true;
        }

        return false;
    }

    private static DecoratorDetectionResult Failure(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] args) =>
        new(null, ImmutableArray.Create(DiagnosticInfo.Create(descriptor, location, args)));
}
