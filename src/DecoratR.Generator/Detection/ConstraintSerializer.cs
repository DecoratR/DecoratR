using System.Collections.Immutable;
using System.Text;
using DecoratR.Generator.Emit;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Serializes the constraints of a decorator's type parameters into strings that survive the trip through
/// assembly metadata, and turns them back into <c>where</c> clauses.
/// </summary>
/// <remarks>
/// Special constraints are encoded as <c>!class</c>, <c>!struct</c>, <c>!notnull</c>, <c>!unmanaged</c> and
/// <c>!new</c>. Type constraints are fully qualified names in which the decorator's own type parameters are
/// replaced by the placeholders <c>{TRequest}</c> and <c>{TResponse}</c>.
/// </remarks>
internal static class ConstraintSerializer
{
    public const string RequestPlaceholder = "{TRequest}";
    public const string ResponsePlaceholder = "{TResponse}";
    public const char Separator = ';';

    public static EquatableArray<string> Serialize(
        ITypeParameterSymbol typeParameter,
        ITypeParameterSymbol requestParameter,
        ITypeParameterSymbol responseParameter)
    {
        var builder = ImmutableArray.CreateBuilder<string>(typeParameter.ConstraintTypes.Length + 3);

        if (typeParameter.HasReferenceTypeConstraint)
            builder.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                ? TypeHierarchy.NullableClassFact
                : TypeHierarchy.ClassFact);
        if (typeParameter.HasUnmanagedTypeConstraint) builder.Add(TypeHierarchy.UnmanagedFact);
        else if (typeParameter.HasValueTypeConstraint) builder.Add(TypeHierarchy.StructFact);
        if (typeParameter.HasNotNullConstraint) builder.Add(TypeHierarchy.NotNullFact);

        foreach (var constraint in typeParameter.ConstraintTypes)
            builder.Add(ToTemplate(constraint, requestParameter, responseParameter));

        if (typeParameter.HasConstructorConstraint) builder.Add(TypeHierarchy.NewFact);

        return builder.ToImmutable();
    }

    /// <summary>
    /// Fully qualified display string in which occurrences of the decorator's type parameters are replaced by
    /// placeholders, e.g. <c>global::App.IQuery&lt;{TResponse}&gt;</c>.
    /// </summary>
    public static string ToTemplate(
        ITypeSymbol type,
        ITypeParameterSymbol requestParameter,
        ITypeParameterSymbol responseParameter)
    {
        var parts = type.ToDisplayParts(SymbolExtensions.FullyQualifiedFormat);
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Kind == SymbolDisplayPartKind.TypeParameterName && part.Symbol is ITypeParameterSymbol parameter)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter, requestParameter))
                {
                    sb.Append(RequestPlaceholder);
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(parameter, responseParameter))
                {
                    sb.Append(ResponsePlaceholder);
                    continue;
                }
            }

            sb.Append(part.ToString());
        }

        return sb.ToString();
    }

    public static string Substitute(string template, string requestType, string responseType) =>
        template.Replace(RequestPlaceholder, requestType).Replace(ResponsePlaceholder, responseType);

    public static bool IsSpecialConstraint(string constraint) => constraint.Length > 0 && constraint[0] == '!';

    /// <summary>
    /// Writes <c>where {typeParameter} : ...</c> for the given constraints (nothing when there are none).
    /// </summary>
    public static void WriteWhereClause(
        SourceWriter writer,
        string typeParameter,
        EquatableArray<string> constraints,
        string requestTypeParameter,
        string responseTypeParameter)
    {
        if (constraints.IsEmpty) return;

        writer.Append("where ").Append(typeParameter).Append(" : ");

        var first = true;
        foreach (var constraint in constraints)
        {
            if (!first) writer.Append(", ");
            first = false;

            writer.Append(constraint switch
            {
                TypeHierarchy.ClassFact => "class",
                TypeHierarchy.NullableClassFact => "class?",
                TypeHierarchy.StructFact => "struct",
                TypeHierarchy.NotNullFact => "notnull",
                TypeHierarchy.UnmanagedFact => "unmanaged",
                TypeHierarchy.NewFact => "new()",
                _ => Substitute(constraint, requestTypeParameter, responseTypeParameter),
            });
        }

        writer.AppendLine();
    }

    public static string Join(EquatableArray<string> items)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < items.Length; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(items[i]);
        }

        return sb.ToString();
    }

    public static EquatableArray<string> Split(string? value)
    {
        if (string.IsNullOrEmpty(value)) return EquatableArray<string>.Empty;

        var segments = value!.Split(Separator);
        var builder = ImmutableArray.CreateBuilder<string>(segments.Length);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Length > 0) builder.Add(trimmed);
        }

        return builder.ToImmutable();
    }
}
