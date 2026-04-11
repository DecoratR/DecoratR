using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

internal static class StreamHandlerDetector
{
    public static IncrementalValuesProvider<HandlerMetadata> BuildProvider(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => GetMetadata(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);
    }

    private static HandlerMetadata? GetMetadata(
        GeneratorSyntaxContext context, CancellationToken cancellationToken = default)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.IsStatic || symbol.TypeParameters.Length > 0) return null;

        return ExtractFromSymbol(symbol);
    }

    private static HandlerMetadata? ExtractFromSymbol(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (!IsStreamRequestHandlerInterface(iface)) continue;

            var isDecorator = false;
            foreach (var attr in symbol.GetAttributes())
                if (attr.AttributeClass is { MetadataName: "DecoratorAttribute", ContainingNamespace: { Name: "DecoratR", ContainingNamespace.IsGlobalNamespace: true } })
                {
                    isDecorator = true;
                    break;
                }

            if (isDecorator) return null;

            var requestType = iface.TypeArguments[0];
            var hierarchy = HandlerDetector.BuildTypeHierarchy(requestType);

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                hierarchy);
        }

        return null;
    }

    internal static bool IsStreamRequestHandlerInterface(INamedTypeSymbol iface)
    {
        if (iface.TypeArguments.Length != 2) return false;

        var original = iface.OriginalDefinition;
        if (original.MetadataName != "IStreamRequestHandler`2") return false;

        var ns = original.ContainingNamespace;
        return ns is { Name: "DecoratR", ContainingNamespace.IsGlobalNamespace: true };
    }
}
