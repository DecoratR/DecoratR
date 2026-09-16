using DecoratR.Generator.Detection;
using DecoratR.Generator.Model;

namespace DecoratR.Generator.Emit;

/// <summary>
/// Decides whether a decorator's serialized constraints are satisfied by a concrete service type.
/// </summary>
internal static class ConstraintMatcher
{
    public static bool Satisfies(
        ServiceTypeInfo service,
        EquatableArray<string> requestConstraints,
        EquatableArray<string> responseConstraints) =>
        SatisfiesAll(service, requestConstraints, service.RequestTypeHierarchy) &&
        SatisfiesAll(service, responseConstraints, service.ResponseTypeHierarchy);

    private static bool SatisfiesAll(ServiceTypeInfo service, EquatableArray<string> constraints, EquatableArray<string> hierarchy)
    {
        foreach (var constraint in constraints)
        {
            if (ConstraintSerializer.IsSpecialConstraint(constraint))
            {
                // Nullability is a compile-time annotation the container does not know about: `notnull` never
                // excludes a handler and `class?` is satisfied by any reference type.
                if (constraint == TypeHierarchy.NotNullFact) continue;
                var fact = constraint == TypeHierarchy.NullableClassFact ? TypeHierarchy.ClassFact : constraint;
                if (!hierarchy.Contains(fact)) return false;
                continue;
            }

            var type = ConstraintSerializer.Substitute(constraint, service.RequestType, service.ResponseType).StripNullableAnnotations();
            if (!ContainsIgnoringNullability(hierarchy, type)) return false;
        }

        return true;
    }

    private static bool ContainsIgnoringNullability(EquatableArray<string> hierarchy, string type)
    {
        foreach (var entry in hierarchy)
            if (entry.StripNullableAnnotations() == type)
                return true;

        return false;
    }
}
