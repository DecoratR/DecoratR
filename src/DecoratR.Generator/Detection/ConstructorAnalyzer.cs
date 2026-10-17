using System.Collections.Immutable;
using System.Globalization;
using DecoratR.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Decides whether generated code can call the constructor of a handler or decorator directly
/// (<c>new T(GetRequiredService&lt;A&gt;(sp), …)</c>) with the same result as <c>ActivatorUtilities</c>.
/// </summary>
/// <remarks>
/// Returns <see langword="null"/> (fallback to <c>ActivatorUtilities</c>, resolved lazily on first use) unless the
/// type has exactly one public constructor or exactly one marked with <c>[ActivatorUtilitiesConstructor]</c>, and every
/// parameter is a plain service, a <c>[FromKeyedServices(key)]</c> service with a constant key, the
/// <see cref="System.IServiceProvider"/> or (decorators only) the inner handler. Optional, <c>ref</c>/<c>out</c>/<c>in</c>,
/// <c>params</c> and <c>[ServiceKey]</c> parameters as well as unset <c>required</c> members fall back.
/// </remarks>
internal static class ConstructorAnalyzer
{
    private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string ActivatorUtilitiesConstructorAttributeName = "ActivatorUtilitiesConstructorAttribute";
    private const string FromKeyedServicesAttributeName = "FromKeyedServicesAttribute";
    private const string ServiceKeyAttributeName = "ServiceKeyAttribute";
    private const string SetsRequiredMembersAttributeName = "SetsRequiredMembersAttribute";

    /// <summary>Analyzes a handler constructor.</summary>
    public static ConstructorPlan? ForHandler(INamedTypeSymbol handler) =>
        Analyze(handler, innerHandlerInterface: null, requestParameter: null, responseParameter: null);

    /// <summary>
    /// Analyzes a decorator constructor. Exactly one parameter must be of type <paramref name="handlerInterface"/>;
    /// parameter types are serialized with the <c>{TRequest}</c>/<c>{TResponse}</c> placeholders.
    /// </summary>
    public static ConstructorPlan? ForDecorator(
        INamedTypeSymbol decorator,
        INamedTypeSymbol handlerInterface,
        ITypeParameterSymbol requestParameter,
        ITypeParameterSymbol responseParameter) =>
        Analyze(decorator, handlerInterface, requestParameter, responseParameter);

    private static ConstructorPlan? Analyze(
        INamedTypeSymbol type,
        INamedTypeSymbol? innerHandlerInterface,
        ITypeParameterSymbol? requestParameter,
        ITypeParameterSymbol? responseParameter)
    {
        var constructor = SelectConstructor(type);
        if (constructor is null) return null;
        if (HasRequiredMembers(type) && !HasAttribute(constructor, SetsRequiredMembersAttributeName, "System.Diagnostics.CodeAnalysis"))
            return null;

        var arguments = ImmutableArray.CreateBuilder<ConstructorArgument>(constructor.Parameters.Length);
        var innerHandlers = 0;

        foreach (var parameter in constructor.Parameters)
        {
            var argument = AnalyzeParameter(parameter, innerHandlerInterface, requestParameter, responseParameter);
            if (argument is null) return null;

            if (argument.Value.Kind == ConstructorArgumentKind.InnerHandler) innerHandlers++;
            arguments.Add(argument.Value);
        }

        // A decorator without (or with several) inner handler parameters keeps the ActivatorUtilities path, which
        // reports the problem at runtime exactly as before.
        if (innerHandlerInterface is not null && innerHandlers != 1) return null;

        return new ConstructorPlan(arguments.MoveToImmutable());
    }

    /// <summary>
    /// The constructor <c>ActivatorUtilities</c> would pick without ambiguity: the only one marked with
    /// <c>[ActivatorUtilitiesConstructor]</c>, otherwise the only public one.
    /// </summary>
    private static IMethodSymbol? SelectConstructor(INamedTypeSymbol type)
    {
        IMethodSymbol? single = null;
        IMethodSymbol? preferred = null;
        var publicCount = 0;
        var preferredCount = 0;

        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public) continue;

            publicCount++;
            single = constructor;

            if (HasAttribute(constructor, ActivatorUtilitiesConstructorAttributeName, DependencyInjectionNamespace))
            {
                preferredCount++;
                preferred = constructor;
            }
        }

        if (preferredCount > 0) return preferredCount == 1 ? preferred : null;
        return publicCount == 1 ? single : null;
    }

    private static ConstructorArgument? AnalyzeParameter(
        IParameterSymbol parameter,
        INamedTypeSymbol? innerHandlerInterface,
        ITypeParameterSymbol? requestParameter,
        ITypeParameterSymbol? responseParameter)
    {
        if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.IsOptional || parameter.HasExplicitDefaultValue)
            return null;

        var type = parameter.Type;
        if (type is IPointerTypeSymbol or IFunctionPointerTypeSymbol || type.IsRefLikeType) return null;

        if (innerHandlerInterface is not null && SymbolEqualityComparer.Default.Equals(type, innerHandlerInterface))
            return new ConstructorArgument(ConstructorArgumentKind.InnerHandler, string.Empty);

        string? key = null;
        foreach (var attribute in parameter.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass ||
                attributeClass.ContainingNamespace?.ToDisplayString() != DependencyInjectionNamespace)
                continue;

            switch (attributeClass.Name)
            {
                case ServiceKeyAttributeName:
                    return null;

                case FromKeyedServicesAttributeName:
                    // Only an explicit, constant key maps to GetRequiredKeyedService; the null key and the inherited
                    // key (.NET 10 lookup modes) depend on the registration and stay with ActivatorUtilities.
                    if (attribute.ConstructorArguments.Length != 1 || !attribute.NamedArguments.IsEmpty) return null;
                    key = FormatKey(attribute.ConstructorArguments[0]);
                    if (key is null) return null;
                    break;
            }
        }

        if (key is null && type is INamedTypeSymbol { Name: "IServiceProvider", TypeArguments.Length: 0 } serviceProvider &&
            serviceProvider.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true })
            return new ConstructorArgument(ConstructorArgumentKind.ServiceProvider, string.Empty);

        // GetRequiredService<T> requires T : notnull; nullability is not part of the runtime service type anyway.
        if (type.IsReferenceType) type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        var typeName = requestParameter is not null && responseParameter is not null
            ? ConstraintSerializer.ToTemplate(type, requestParameter, responseParameter)
            : type.ToFullyQualifiedName();

        return key is null
            ? new ConstructorArgument(ConstructorArgumentKind.Service, typeName)
            : new ConstructorArgument(ConstructorArgumentKind.KeyedService, typeName, key);
    }

    /// <summary>The C# expression of a constant service key, or <see langword="null"/> when it cannot be emitted.</summary>
    private static string? FormatKey(TypedConstant key)
    {
        if (key.IsNull) return null;

        switch (key.Kind)
        {
            case TypedConstantKind.Primitive:
                return key.Value switch
                {
                    string value => SymbolDisplay.FormatLiteral(value, quote: true),
                    char value => SymbolDisplay.FormatLiteral(value, quote: true),
                    bool value => value ? "true" : "false",
                    int value => value.ToString(CultureInfo.InvariantCulture),
                    // The cast keeps the boxed key's runtime type identical to the attribute argument.
                    sbyte or byte or short or ushort or uint or long or ulong =>
                        "(" + key.Type!.ToDisplayString() + ")" + Convert.ToString(key.Value, CultureInfo.InvariantCulture),
                    _ => null,
                };

            case TypedConstantKind.Enum when key.Type is INamedTypeSymbol enumType && enumType.IsAccessibleFromGeneratedCode():
                return "(" + enumType.ToFullyQualifiedName() + ")(" + Convert.ToString(key.Value, CultureInfo.InvariantCulture) + ")";

            case TypedConstantKind.Type when key.Value is INamedTypeSymbol { IsUnboundGenericType: false } typeKey &&
                                            typeKey.IsAccessibleFromGeneratedCode():
                return "typeof(" + typeKey.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToFullyQualifiedName() + ")";

            default:
                return null;
        }
    }

    /// <summary>
    /// Whether the type or one of its base types declares <c>required</c> members; <c>new T(…)</c> does not compile
    /// for such types unless the constructor is marked with <c>[SetsRequiredMembers]</c>.
    /// </summary>
    private static bool HasRequiredMembers(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName, string attributeNamespace)
    {
        foreach (var attribute in symbol.GetAttributes())
            if (attribute.AttributeClass is { } attributeClass &&
                attributeClass.Name == attributeName &&
                attributeClass.ContainingNamespace?.ToDisplayString() == attributeNamespace)
                return true;

        return false;
    }
}
