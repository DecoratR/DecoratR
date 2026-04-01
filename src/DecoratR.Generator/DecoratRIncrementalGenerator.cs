using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DecoratRIncrementalGenerator : IIncrementalGenerator
{
    private const string RequestHandlerMetadataName = "DecoratR.IRequestHandler`2";
    private const string CommandMetadataName = "DecoratR.ICommand`1";
    private const string QueryMetadataName = "DecoratR.IQuery`1";
    private const string AttributeMetadataName = "DecoratR.GenerateHandlerRegistrationsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Emit the marker attribute
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("GenerateHandlerRegistrationsAttribute.g.cs",
                SourceGenerationHelper.GenerateAttribute()));

        // Step 2: Check if the assembly attribute is present
        var hasAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                predicate: static (node, _) => true,
                transform: static (ctx, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        // Step 3: Find all handler classes
        var handlerMetadata = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetHandlerMetadata(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var collectedHandlers = handlerMetadata.Collect();

        // Step 4: Combine attribute check with handlers and generate
        var combined = hasAttribute
            .Combine(collectedHandlers)
            .Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown"));

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((hasAttr, handlers), assemblyName) = source;

            if (!hasAttr)
                return;

            if (handlers.Length == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.NoHandlersFound,
                    Location.None,
                    assemblyName));
                return;
            }

            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.HandlersDiscovered,
                Location.None,
                handlers.Length,
                assemblyName));

            var sorted = handlers
                .OrderBy(h => h.HandlerFullyQualifiedName)
                .ToList();

            var source_text = SourceGenerationHelper.GenerateRegistrations(assemblyName, sorted);
            spc.AddSource("DecoratRHandlerRegistrations.g.cs", source_text);
        });
    }

    private static HandlerMetadata? GetHandlerMetadata(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
            return null;

        // Skip abstract, static, and open generic types
        if (symbol.IsAbstract || symbol.IsStatic)
            return null;

        if (symbol.TypeParameters.Length > 0)
            return null;

        // Find IRequestHandler<TRequest, TResponse> implementation
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() != "DecoratR.IRequestHandler<TRequest, TResponse>")
                continue;

            if (iface.TypeArguments.Length != 2)
                continue;

            var requestType = iface.TypeArguments[0];
            var responseType = iface.TypeArguments[1];

            var category = ClassifyRequest(requestType);

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                category);
        }

        return null;
    }

    private static RequestCategory ClassifyRequest(ITypeSymbol requestType)
    {
        foreach (var iface in requestType.AllInterfaces)
        {
            var name = iface.OriginalDefinition.ToDisplayString();
            if (name == "DecoratR.ICommand<TResponse>")
                return RequestCategory.Command;
            if (name == "DecoratR.IQuery<TResponse>")
                return RequestCategory.Query;
        }

        return RequestCategory.Request;
    }
}
