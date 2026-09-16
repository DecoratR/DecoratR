using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Serializes everything about a request or response type that decorator constraints can be matched against:
/// the type itself, its interfaces, its base types and a few type facts.
/// </summary>
internal static class TypeHierarchy
{
    public const string ClassFact = "!class";
    public const string NullableClassFact = "!class?";
    public const string StructFact = "!struct";
    public const string NotNullFact = "!notnull";
    public const string UnmanagedFact = "!unmanaged";
    public const string NewFact = "!new";

    public static EquatableArray<string> Build(ITypeSymbol type, Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<string>(type.AllInterfaces.Length + 6);

        builder.Add(type.ToFullyQualifiedName());

        // Includes the DecoratR marker interfaces, so `where TRequest : IRequest` matches like any other constraint.
        // Interfaces that are not accessible from this compilation (e.g. internal BCL interfaces) can never appear
        // in a constraint here and are skipped; internal interfaces of this assembly are kept.
        foreach (var iface in type.AllInterfaces)
            if (compilation.IsSymbolAccessibleWithin(iface, compilation.Assembly))
                builder.Add(iface.ToFullyQualifiedName());

        for (var baseType = type.BaseType;
             baseType is not null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
            builder.Add(baseType.ToFullyQualifiedName());

        if (type.IsReferenceType) builder.Add(ClassFact);
        if (type.IsValueType) builder.Add(StructFact);
        if (type.IsUnmanagedType) builder.Add(UnmanagedFact);
        if (HasPublicParameterlessConstructor(type)) builder.Add(NewFact);

        return builder.ToImmutable();
    }

    private static bool HasPublicParameterlessConstructor(ITypeSymbol type)
    {
        if (type.IsValueType) return true;
        if (type is not INamedTypeSymbol { IsAbstract: false } named) return false;

        foreach (var constructor in named.InstanceConstructors)
            if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public)
                return true;

        return false;
    }
}
