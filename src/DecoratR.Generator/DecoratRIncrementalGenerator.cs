using System.Collections.Immutable;
using DecoratR.Generator.Detection;
using DecoratR.Generator.Emit;
using DecoratR.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

/// <summary>Names of the tracked incremental steps (used by incremental caching tests).</summary>
internal static class TrackingNames
{
    public const string Handlers = "DecoratR.Handlers";
    public const string Decorators = "DecoratR.Decorators";
    public const string Referenced = "DecoratR.Referenced";
    public const string MetadataInput = "DecoratR.MetadataInput";
    public const string RegistrationsInput = "DecoratR.RegistrationsInput";
}

[Generator(LanguageNames.CSharp)]
public sealed class DecoratRIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assembly = context.CompilationProvider
            .Select(static (compilation, _) => AssemblyInfo.Create(compilation.AssemblyName));

        var hasServiceCollection = context.CompilationProvider
            .Select(static (compilation, _) =>
                compilation.GetTypeByMetadataName(WellKnownTypes.ServiceCollectionMetadataName) is not null);

        // One syntactic pass over candidate type declarations finds request and stream handlers alike.
        var handlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => HandlerDetector.IsCandidate(node),
                static (ctx, ct) => HandlerDetector.Detect(ctx, ct))
            .Where(static result => result is not null)
            .Collect()
            .Select(static (results, _) => HandlerDetector.Aggregate(results))
            .WithTrackingName(TrackingNames.Handlers);

        var decorators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WellKnownTypes.DecoratorAttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, ct) => DecoratorDetector.Detect(ctx, ct))
            .Collect()
            .Select(static (results, _) => DecoratorDetector.Aggregate(results))
            .WithTrackingName(TrackingNames.Decorators);

        var metadataTrigger = FindTrigger(context, WellKnownTypes.GenerateMetadataAttributeMetadataName);
        var registrationsTrigger = FindTrigger(context, WellKnownTypes.GenerateRegistrationsAttributeMetadataName);

        // Referenced metadata is only needed (and only scanned) in composition roots.
        var referenced = registrationsTrigger
            .Combine(context.CompilationProvider)
            .Select(static (pair, ct) => pair.Left.IsPresent
                ? ReferencedAssemblyScanner.Scan(pair.Right, ct)
                : ReferencedRegistrationData.Empty)
            .WithTrackingName(TrackingNames.Referenced);

        var detectionDiagnostics = handlers
            .Combine(decorators)
            .Combine(metadataTrigger)
            .Combine(registrationsTrigger)
            .Select(static (t, _) => new DetectionDiagnosticsInput(
                t.Left.Left.Left.Diagnostics,
                t.Left.Left.Right.Diagnostics,
                t.Left.Right.IsPresent || t.Right.IsPresent));

        context.RegisterSourceOutput(detectionDiagnostics, static (spc, input) => ReportDetectionDiagnostics(spc, input));

        var metadataInput = metadataTrigger
            .Combine(handlers)
            .Combine(decorators)
            .Combine(assembly)
            .Combine(hasServiceCollection)
            .Select(static (t, _) => new MetadataInput(
                t.Left.Left.Left.Left,
                t.Left.Left.Left.Right,
                t.Left.Left.Right,
                t.Left.Right,
                t.Right))
            .WithTrackingName(TrackingNames.MetadataInput);

        context.RegisterSourceOutput(metadataInput, static (spc, input) => EmitMetadata(spc, input));

        var registrationsInput = registrationsTrigger
            .Combine(handlers)
            .Combine(decorators)
            .Combine(referenced)
            .Combine(assembly)
            .Combine(hasServiceCollection)
            .Select(static (t, _) => new RegistrationsInput(
                t.Left.Left.Left.Left.Left,
                t.Left.Left.Left.Left.Right,
                t.Left.Left.Left.Right,
                t.Left.Left.Right,
                t.Left.Right,
                t.Right))
            .WithTrackingName(TrackingNames.RegistrationsInput);

        context.RegisterSourceOutput(registrationsInput, static (spc, input) => EmitRegistrations(spc, input));
    }

    private static IncrementalValueProvider<GenerationTrigger> FindTrigger(
        IncrementalGeneratorInitializationContext context,
        string attributeMetadataName)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                attributeMetadataName,
                static (_, _) => true,
                static (ctx, ct) => new GenerationTrigger(
                    true,
                    LocationInfo.From(ctx.Attributes[0].ApplicationSyntaxReference?.GetSyntax(ct))))
            .Collect()
            .Select(static (triggers, _) => triggers.Length > 0 ? triggers[0] : GenerationTrigger.Absent);
    }

    private static void ReportDetectionDiagnostics(SourceProductionContext spc, DetectionDiagnosticsInput input)
    {
        // Detection diagnostics are only meaningful in assemblies that actually use DecoratR generation.
        if (!input.AnyTriggerPresent) return;

        foreach (var diagnostic in input.HandlerDiagnostics) spc.ReportDiagnostic(diagnostic.ToDiagnostic());
        foreach (var diagnostic in input.DecoratorDiagnostics) spc.ReportDiagnostic(diagnostic.ToDiagnostic());
    }

    private static void EmitMetadata(SourceProductionContext spc, MetadataInput input)
    {
        if (!input.Trigger.IsPresent) return;

        var handlers = input.Handlers.Handlers;
        var decorators = input.Decorators.Decorators;

        if (handlers.IsEmpty && decorators.IsEmpty)
        {
            Report(spc, Diagnostics.NothingFound, input.Trigger.Location,
                input.Assembly.Name, WellKnownTypes.GenerateMetadataAttributeShortName);
            return;
        }

        ReportDuplicateHandlers(spc, handlers, EquatableArray<HandlerMetadata>.Empty);

        foreach (var handler in handlers)
            if (!handler.ServiceType.IsPubliclyAccessible)
                Report(spc, Diagnostics.HandlerServiceTypeNotPublic, handler.Location,
                    Display(handler.HandlerType), Display(handler.ServiceType.ConstructedInterface));

        if (!handlers.IsEmpty)
        {
            Report(spc, Diagnostics.HandlersDiscovered, null, handlers.Length, input.Assembly.Name);
            spc.AddSource(HandlerRegistryEmitter.HintName, HandlerRegistryEmitter.Generate(input.Assembly, handlers));
        }

        if (decorators.IsEmpty) return;

        Report(spc, Diagnostics.DecoratorsDiscovered, null, decorators.Length, input.Assembly.Name);

        // The decorator registry wraps ServiceDescriptors and therefore needs the DI abstractions.
        if (!input.HasServiceCollection)
        {
            Report(spc, Diagnostics.MissingDependencyInjection, input.Trigger.Location,
                input.Assembly.Name, WellKnownTypes.GenerateMetadataAttributeShortName);
            return;
        }

        // Public apply methods cannot carry non-public constraint types (CS0703); such decorators stay local.
        var exportable = new List<DecoratorMetadata>(decorators.Length);
        foreach (var decorator in decorators)
        {
            if (decorator.NonPublicConstraintType is { } constraintType)
            {
                Report(spc, Diagnostics.DecoratorConstraintNotPublic, decorator.Location,
                    Display(decorator.DecoratorType), constraintType);
                continue;
            }

            exportable.Add(decorator);
        }

        if (exportable.Count > 0)
            spc.AddSource(DecoratorRegistryEmitter.HintName,
                DecoratorRegistryEmitter.Generate(input.Assembly, exportable.ToImmutableArray()));
    }

    private static void EmitRegistrations(SourceProductionContext spc, RegistrationsInput input)
    {
        if (!input.Trigger.IsPresent) return;

        if (!input.HasServiceCollection)
        {
            Report(spc, Diagnostics.MissingDependencyInjection, input.Trigger.Location,
                input.Assembly.Name, WellKnownTypes.GenerateRegistrationsAttributeShortName);
            return;
        }

        var localHandlers = input.Handlers.Handlers;
        var localDecorators = input.Decorators.Decorators;
        var referenced = input.Referenced;

        var handlerCount = localHandlers.Length + referenced.Handlers.Length;
        var decoratorCount = localDecorators.Length + referenced.Decorators.Length;

        if (handlerCount == 0 && decoratorCount == 0)
        {
            Report(spc, Diagnostics.NothingFound, input.Trigger.Location,
                input.Assembly.Name, WellKnownTypes.GenerateRegistrationsAttributeShortName);
        }
        else
        {
            if (handlerCount > 0) Report(spc, Diagnostics.HandlersDiscovered, null, handlerCount, input.Assembly.Name);
            if (decoratorCount > 0) Report(spc, Diagnostics.DecoratorsDiscovered, null, decoratorCount, input.Assembly.Name);
        }

        ReportDuplicateHandlers(spc, localHandlers, referenced.Handlers);

        var plan = DecorationPlanner.Plan(localHandlers, referenced, localDecorators);

        foreach (var skipped in plan.SkippedNonPublicServices)
            Report(spc, Diagnostics.ServiceTypeNotPublic, null, Display(skipped.ConstructedInterface));

        spc.AddSource(RegistrationsEmitter.HintName,
            RegistrationsEmitter.Generate(input.Assembly, localHandlers, referenced, plan, decoratorCount));
    }

    /// <summary>
    /// Reports DCTR007 for every service type implemented by more than one handler type. Only the last DI
    /// registration would be resolved, so this is a configuration error.
    /// </summary>
    private static void ReportDuplicateHandlers(
        SourceProductionContext spc,
        EquatableArray<HandlerMetadata> localHandlers,
        EquatableArray<HandlerMetadata> referencedHandlers)
    {
        var all = new List<HandlerMetadata>(localHandlers.Length + referencedHandlers.Length);
        all.AddRange(localHandlers);
        all.AddRange(referencedHandlers);
        all.Sort(HandlerMetadata.Compare);

        var start = 0;
        while (start < all.Count)
        {
            var end = start + 1;
            while (end < all.Count && ServiceTypeInfo.SameService(all[start].ServiceType, all[end].ServiceType)) end++;

            if (end - start > 1)
            {
                var handlerTypes = new List<string>();
                LocationInfo? location = null;

                for (var i = start; i < end; i++)
                {
                    var display = Display(all[i].HandlerType);
                    if (!handlerTypes.Contains(display)) handlerTypes.Add(display);
                    location ??= all[i].Location;
                }

                if (handlerTypes.Count > 1)
                    Report(spc, Diagnostics.DuplicateHandler, location,
                        Display(all[start].ServiceType.ConstructedInterface), string.Join(", ", handlerTypes));
            }

            start = end;
        }
    }

    private static void Report(SourceProductionContext spc, DiagnosticDescriptor descriptor, LocationInfo? location, params object[] args) =>
        spc.ReportDiagnostic(Diagnostic.Create(descriptor, location?.ToLocation() ?? Location.None, args));

    private static string Display(string fullyQualifiedName) => fullyQualifiedName.Replace("global::", string.Empty);
}
