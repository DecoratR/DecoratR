using DecoratR.Generator.Model;

namespace DecoratR.Generator.Emit;

/// <summary>
/// One decorator application step: a call of a generated apply method, either a private one in the composition root
/// (local decorator) or the public one of a referenced decorator registry (fully qualified).
/// </summary>
internal sealed record DecoratorStep(int Order, string DecoratorType, string ApplyMethod);

/// <summary>The decorators applied to one service type, innermost first.</summary>
internal sealed record ServicePipeline(ServiceTypeInfo Service, IReadOnlyList<DecoratorStep> Steps);

/// <summary>A local decorator used by at least one pipeline, with the name of its private apply method.</summary>
internal sealed record LocalApplyMethod(DecoratorMetadata Decorator, string Name);

/// <summary>Result of <see cref="DecorationPlanner.Plan"/>.</summary>
internal sealed record DecorationPlan(
    IReadOnlyList<ServicePipeline> Pipelines,
    IReadOnlyList<ServiceTypeInfo> SkippedNonPublicServices,
    IReadOnlyList<LocalApplyMethod> LocalApplyMethods);

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
        int LocalIndex,
        string ApplyMethod);

    public static DecorationPlan Plan(
        EquatableArray<HandlerMetadata> localHandlers,
        ReferencedRegistrationData referenced,
        EquatableArray<DecoratorMetadata> localDecorators)
    {
        var localNames = DecoratorRegistryEmitter.ApplyMethodNames(localDecorators);
        var candidates = new List<Candidate>(localDecorators.Length + referenced.Decorators.Length);
        for (var i = 0; i < localDecorators.Length; i++)
        {
            var d = localDecorators[i];
            candidates.Add(new Candidate(d.Order, d.DecoratorType, d.IsStream, d.RequestConstraints, d.ResponseConstraints, i, localNames[i]));
        }

        foreach (var d in referenced.Decorators)
            candidates.Add(new Candidate(d.Order, d.DecoratorType, d.IsStream, d.RequestConstraints, d.ResponseConstraints, -1, d.ApplyMethod));

        // Lower Order = further outside; ties are broken by the fully qualified decorator type name for local
        // and referenced decorators alike.
        candidates.Sort(static (a, b) =>
        {
            var cmp = a.Order.CompareTo(b.Order);
            if (cmp != 0) return cmp;

            cmp = string.CompareOrdinal(a.DecoratorType, b.DecoratorType);
            // Identical type names from different assemblies: keep the order deterministic via the apply method.
            return cmp != 0 ? cmp : string.CompareOrdinal(a.ApplyMethod, b.ApplyMethod);
        });

        // Distinct service types. Local handlers come first so a service type declared locally is never treated
        // as a referenced one.
        var services = new List<(ServiceTypeInfo Service, bool IsLocal)>(localHandlers.Length + referenced.Handlers.Length);
        foreach (var handler in localHandlers) AddDistinct(services, handler.ServiceType, isLocal: true);
        foreach (var handler in referenced.Handlers) AddDistinct(services, handler.ServiceType, isLocal: false);
        services.Sort(static (a, b) => ServiceTypeInfo.Compare(a.Service, b.Service));

        var pipelines = new List<ServicePipeline>();
        var skipped = new List<ServiceTypeInfo>();
        var usedLocalDecorators = new bool[localDecorators.Length];

        var matches = new List<Candidate>();

        foreach (var (service, isLocal) in services)
        {
            matches.Clear();

            // Iterate from the highest Order (innermost, wrapped first) to the lowest (outermost, wrapped last).
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                if (candidate.IsStream != service.IsStream) continue;
                if (!ConstraintMatcher.Satisfies(service, candidate.RequestConstraints, candidate.ResponseConstraints)) continue;

                matches.Add(candidate);
            }

            if (matches.Count == 0) continue;

            if (!isLocal && !service.IsPubliclyAccessible)
            {
                skipped.Add(service);
                continue;
            }

            var steps = new DecoratorStep[matches.Count];
            for (var i = 0; i < steps.Length; i++)
            {
                var match = matches[i];
                steps[i] = new DecoratorStep(match.Order, match.DecoratorType, match.ApplyMethod);
                if (match.LocalIndex >= 0) usedLocalDecorators[match.LocalIndex] = true;
            }

            pipelines.Add(new ServicePipeline(service, steps));
        }

        var localApplyMethods = new List<LocalApplyMethod>();
        for (var i = 0; i < localDecorators.Length; i++)
            if (usedLocalDecorators[i])
                localApplyMethods.Add(new LocalApplyMethod(localDecorators[i], localNames[i]));

        return new DecorationPlan(pipelines, skipped, localApplyMethods);
    }

    private static void AddDistinct(List<(ServiceTypeInfo Service, bool IsLocal)> services, ServiceTypeInfo service, bool isLocal)
    {
        foreach (var existing in services)
            if (ServiceTypeInfo.SameService(existing.Service, service))
                return;

        services.Add((service, isLocal));
    }
}
