using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DecoratRIncrementalGenerator : IIncrementalGenerator
{
    private const string HandlerAttributeMetadataName = "DecoratR.GenerateHandlerRegistrationsAttribute";
    private const string FullAttributeMetadataName = "DecoratR.GenerateDecoratRRegistrationsAttribute";
    private const string DecoratorAttributeMetadataName = "DecoratR.DecoratorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        RegisterAttributes(context);
        RegisterHandlerOnlyPath(context);
        RegisterFullPath(context);
    }

    private static void RegisterAttributes(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenerateHandlerRegistrationsAttribute.g.cs",
                AttributeEmitter.GenerateHandlerAttribute());
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

    private static void RegisterHandlerOnlyPath(IncrementalGeneratorInitializationContext context)
    {
        var hasAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                HandlerAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

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

        var combined = hasAttribute
            .Combine(localHandlers)
            .Combine(localDecorators)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (((hasAttr, handlers), decorators), name) = source;
            if (!hasAttr)
            {
                return;
            }

            var sortedDecorators = decorators
                .Distinct()
                .OrderBy(d => d.Order)
                .ThenBy(d => d.DecoratorFullyQualifiedName)
                .ToArray();

            EmitHandlerRegistry(spc, name, handlers, sortedDecorators);
        });
    }

    private static void RegisterFullPath(IncrementalGeneratorInitializationContext context)
    {
        var hasAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FullAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        var localHandlers = HandlerDetector.BuildProvider(context).Collect();

        var localDecorators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DecoratorAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => DecoratorDetector.GetMetadata(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        var referencedRegistrations = context.CompilationProvider
            .Select(static (compilation, ct) => ReferencedAssemblyScanner.Scan(compilation, ct));

        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown");

        var combined = hasAttribute
            .Combine(localHandlers)
            .Combine(referencedRegistrations)
            .Combine(localDecorators)
            .Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((((hasAttr, localH), referenced), decorators), name) = source;
            if (!hasAttr)
            {
                return;
            }

            var sortedLocalHandlers = localH
                .Distinct()
                .OrderBy(h => h.HandlerFullyQualifiedName)
                .ToArray();

            var sortedDecorators = decorators
                .Distinct()
                .OrderBy(d => d.Order)
                .ThenBy(d => d.DecoratorFullyQualifiedName)
                .ToArray();

            EmitFullRegistrations(spc, name, sortedLocalHandlers, referenced, sortedDecorators);
        });
    }

    private static void EmitHandlerRegistry(
        SourceProductionContext spc,
        string assemblyName,
        ImmutableArray<HandlerMetadata> handlers,
        IReadOnlyList<DecoratorMetadata> decorators)
    {
        if (handlers.Length == 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NoHandlersFound, Location.None, assemblyName));
            return;
        }

        spc.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.HandlersDiscovered, Location.None, handlers.Length, assemblyName));

        var sorted = handlers.OrderBy(h => h.HandlerFullyQualifiedName).ToList();
        spc.AddSource("DecoratRHandlerRegistrations.g.cs",
            HandlerRegistryEmitter.Generate(assemblyName, sorted, decorators));
    }

    private static void EmitFullRegistrations(
        SourceProductionContext spc,
        string assemblyName,
        IReadOnlyList<HandlerMetadata> localHandlers,
        ReferencedRegistrationData referenced,
        IReadOnlyList<DecoratorMetadata> localDecorators)
    {
        var totalHandlerCount = localHandlers.Count + referenced.ServiceTypes.Length;

        if (totalHandlerCount == 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NoHandlersFound, Location.None, assemblyName));
        }
        else
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.HandlersDiscovered, Location.None, totalHandlerCount, assemblyName));
        }

        var totalDecoratorCount = localDecorators.Count + referenced.Decorators.Length;
        if (totalDecoratorCount > 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DecoratorsDiscovered, Location.None, totalDecoratorCount, assemblyName));
        }

        spc.AddSource("DecoratRRegistrations.g.cs",
            FullRegistrationEmitter.Generate(assemblyName, localHandlers,
                referenced.RegistryClassNames, referenced.ServiceTypes,
                localDecorators, referenced.Decorators));
    }
}