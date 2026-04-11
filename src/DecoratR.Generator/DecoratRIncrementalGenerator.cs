using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DecoratRIncrementalGenerator : IIncrementalGenerator
{
    private const string MetadataAttributeMetadataName = "DecoratR.GenerateDecoratRMetadataAttribute";
    private const string FullAttributeMetadataName = "DecoratR.GenerateDecoratRRegistrationsAttribute";
    private const string DecoratorAttributeMetadataName = "DecoratR.DecoratorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        RegisterAttributes(context);

        // Build shared providers once — both paths reuse the same syntax tree scan
        var localHandlers = HandlerDetector.BuildProvider(context).Collect();

        var localDecorators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DecoratorAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => DecoratorDetector.GetMetadata(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown");

        RegisterHandlerOnlyPath(context, localHandlers, localDecorators, assemblyName);
        RegisterFullPath(context, localHandlers, localDecorators, assemblyName);
    }

    private static void RegisterAttributes(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenerateDecoratRMetadataAttribute.g.cs",
                AttributeEmitter.GenerateMetadataAttribute());
            ctx.AddSource("GenerateDecoratRRegistrationsAttribute.g.cs",
                AttributeEmitter.GenerateFullAttribute());
            ctx.AddSource("DecoratRHandlerRegistrationAttribute.g.cs",
                AttributeEmitter.GenerateHandlerRegistrationAttribute());
            ctx.AddSource("DecoratRHandlerServiceTypeAttribute.g.cs",
                AttributeEmitter.GenerateHandlerServiceTypeAttribute());
            ctx.AddSource("DecoratRDecoratorRegistrationAttribute.g.cs",
                AttributeEmitter.GenerateDecoratorRegistrationAttribute());
        });
    }

    private static void RegisterHandlerOnlyPath(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<HandlerMetadata>> localHandlers,
        IncrementalValueProvider<ImmutableArray<DecoratorMetadata>> localDecorators,
        IncrementalValueProvider<string> assemblyName)
    {
        var hasAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        var combined = hasAttribute
            .Combine(localHandlers)
            .Combine(localDecorators)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (((hasAttr, handlers), decorators), name) = source;
            if (!hasAttr) return;

            var sortedDecorators = SortDecorators(decorators);
            EmitHandlerRegistry(spc, name, handlers, sortedDecorators);
        });
    }

    private static void RegisterFullPath(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<HandlerMetadata>> localHandlers,
        IncrementalValueProvider<ImmutableArray<DecoratorMetadata>> localDecorators,
        IncrementalValueProvider<string> assemblyName)
    {
        var hasAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FullAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        var referencedRegistrations = context.CompilationProvider
            .Select(static (compilation, ct) => ReferencedAssemblyScanner.Scan(compilation, ct));

        var combined = hasAttribute
            .Combine(localHandlers)
            .Combine(referencedRegistrations)
            .Combine(localDecorators)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((((hasAttr, localH), referenced), decorators), name) = source;
            if (!hasAttr) return;

            var sortedLocalHandlers = SortHandlers(localH);
            var sortedDecorators = SortDecorators(decorators);

            EmitFullRegistrations(spc, name, sortedLocalHandlers, referenced, sortedDecorators);
        });
    }

    private static HandlerMetadata[] SortHandlers(ImmutableArray<HandlerMetadata> handlers)
    {
        if (handlers.Length == 0) return Array.Empty<HandlerMetadata>();

        var array = new HandlerMetadata[handlers.Length];
        handlers.CopyTo(array, 0);
        Array.Sort(array, static (a, b) =>
            string.Compare(a.HandlerFullyQualifiedName, b.HandlerFullyQualifiedName, StringComparison.Ordinal));
        return array;
    }

    private static DecoratorMetadata[] SortDecorators(ImmutableArray<DecoratorMetadata> decorators)
    {
        if (decorators.Length == 0) return Array.Empty<DecoratorMetadata>();

        var array = new DecoratorMetadata[decorators.Length];
        decorators.CopyTo(array, 0);
        Array.Sort(array, static (a, b) =>
        {
            var cmp = a.Order.CompareTo(b.Order);
            return cmp != 0
                ? cmp
                : string.Compare(a.DecoratorFullyQualifiedName, b.DecoratorFullyQualifiedName,
                    StringComparison.Ordinal);
        });
        return array;
    }

    private static void EmitHandlerRegistry(
        SourceProductionContext spc,
        string assemblyName,
        ImmutableArray<HandlerMetadata> handlers,
        DecoratorMetadata[] decorators)
    {
        if (handlers.Length == 0 && decorators.Length == 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NothingFound, Location.None, assemblyName));
            return;
        }

        if (handlers.Length > 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.HandlersDiscovered, Location.None, handlers.Length, assemblyName));

            var sorted = SortHandlers(handlers);
            spc.AddSource("DecoratRHandlerRegistrations.g.cs",
                HandlerRegistryEmitter.Generate(assemblyName, sorted));
        }

        if (decorators.Length > 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DecoratorsDiscovered, Location.None, decorators.Length, assemblyName));

            spc.AddSource("DecoratRDecoratorRegistrations.g.cs",
                DecoratorRegistryEmitter.Generate(assemblyName, decorators));
        }
    }

    private static void EmitFullRegistrations(
        SourceProductionContext spc,
        string assemblyName,
        HandlerMetadata[] localHandlers,
        ReferencedRegistrationData referenced,
        DecoratorMetadata[] localDecorators)
    {
        var totalHandlerCount = localHandlers.Length + referenced.ServiceTypes.Length;
        var totalDecoratorCount = localDecorators.Length + referenced.Decorators.Length;

        if (totalHandlerCount == 0 && totalDecoratorCount == 0)
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NothingFound, Location.None, assemblyName));

        if (totalHandlerCount > 0)
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.HandlersDiscovered, Location.None, totalHandlerCount, assemblyName));

        if (totalDecoratorCount > 0)
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DecoratorsDiscovered, Location.None, totalDecoratorCount, assemblyName));

        spc.AddSource("DecoratROptions.g.cs",
            AttributeEmitter.GenerateOptions());

        spc.AddSource("DecoratRRegistrations.g.cs",
            FullRegistrationEmitter.Generate(assemblyName, localHandlers,
                referenced.RegistryClassNames, referenced.ServiceTypes,
                localDecorators, referenced.Decorators));
    }
}