using DecoratR.Generator.Model;

namespace DecoratR.Generator.Emit;

/// <summary>One decorator application step: either a local <c>Wrap</c> call or a referenced apply method.</summary>
internal sealed record DecoratorStep(int Order, string DecoratorType, string? LocalConstructedType, string? ApplyMethod);

/// <summary>The decorators applied to one service type, innermost first.</summary>
internal sealed record ServicePipeline(ServiceTypeInfo Service, IReadOnlyList<DecoratorStep> Steps);

/// <summary>Result of <see cref="DecorationPlanner.Plan"/>.</summary>
internal sealed record DecorationPlan(
    IReadOnlyList<ServicePipeline> Pipelines,
    IReadOnlyList<ServiceTypeInfo> SkippedNonPublicServices,
    bool UsesLocalDecorators);

/// <summary>
/// Combines local and referenced handlers and decorators into per-service-type decorator pipelines.
/// </summary>
internal static class DecorationPlanner
{
    private sealed record Candidate(
        int Order,
        string DecoratorType,
        bool IsStream,
        EquatableArray<string> RequestConstraints,
        EquatableArray<string> ResponseConstraints,
        DecoratorMetadata? Local,
        string? ApplyMethod);

    public static DecorationPlan Plan(
        EquatableArray<HandlerMetadata> localHandlers,
        ReferencedRegistrationData referenced,
        EquatableArray<DecoratorMetadata> localDecorators)
    {
        var candidates = new List<Candidate>(localDecorators.Length + referenced.Decorators.Length);
        foreach (var d in localDecorators)
            candidates.Add(new Candidate(d.Order, d.DecoratorType, d.IsStream, d.RequestConstraints, d.ResponseConstraints, d, null));
        foreach (var d in referenced.Decorators)
            candidates.Add(new Candidate(d.Order, d.DecoratorType, d.IsStream, d.RequestConstraints, d.ResponseConstraints, null, d.ApplyMethod));

        // Lower Order = further outside; ties are broken by the fully qualified decorator type name for local
        // and referenced decorators alike.
        candidates.Sort(static (a, b) =>
        {
            var cmp = a.Order.CompareTo(b.Order);
            if (cmp != 0) return cmp;

            cmp = string.CompareOrdinal(a.DecoratorType, b.DecoratorType);
            // Identical type names from different assemblies: keep the order deterministic via the apply method.
            return cmp != 0 ? cmp : string.CompareOrdinal(a.ApplyMethod ?? string.Empty, b.ApplyMethod ?? string.Empty);
        });

        // Distinct service types. Local handlers come first so a service type declared locally is never treated
        // as a referenced one.
        var services = new List<(ServiceTypeInfo Service, bool IsLocal)>(localHandlers.Length + referenced.Handlers.Length);
        foreach (var handler in localHandlers) AddDistinct(services, handler.ServiceType, isLocal: true);
        foreach (var handler in referenced.Handlers) AddDistinct(services, handler.ServiceType, isLocal: false);
        services.Sort(static (a, b) => ServiceTypeInfo.Compare(a.Service, b.Service));

        var pipelines = new List<ServicePipeline>();
        var skipped = new List<ServiceTypeInfo>();
        var usesLocalDecorators = false;

        foreach (var (service, isLocal) in services)
        {
            var steps = new List<DecoratorStep>();

            // Iterate from the highest Order (innermost, wrapped first) to the lowest (outermost, wrapped last).
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                if (candidate.IsStream != service.IsStream) continue;
                if (!ConstraintMatcher.Satisfies(service, candidate.RequestConstraints, candidate.ResponseConstraints)) continue;

                steps.Add(new DecoratorStep(
                    candidate.Order,
                    candidate.DecoratorType,
                    candidate.Local?.Construct(service.RequestType, service.ResponseType),
                    candidate.ApplyMethod));
            }

            if (steps.Count == 0) continue;

            if (!isLocal && !service.IsPubliclyAccessible)
            {
                skipped.Add(service);
                continue;
            }

            foreach (var step in steps)
                if (step.LocalConstructedType is not null)
                    usesLocalDecorators = true;

            pipelines.Add(new ServicePipeline(service, steps));
        }

        return new DecorationPlan(pipelines, skipped, usesLocalDecorators);
    }

    private static void AddDistinct(List<(ServiceTypeInfo Service, bool IsLocal)> services, ServiceTypeInfo service, bool isLocal)
    {
        foreach (var existing in services)
            if (ServiceTypeInfo.SameService(existing.Service, service))
                return;

        services.Add((service, isLocal));
    }
}
