using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

internal static class HandlerDetector
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
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax) context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (symbol.IsAbstract || symbol.IsStatic || symbol.TypeParameters.Length > 0)
        {
            return null;
        }

        return ExtractFromSymbol(symbol);
    }

    private static HandlerMetadata? ExtractFromSymbol(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() != "DecoratR.IRequestHandler<TRequest, TResponse>")
            {
                continue;
            }

            if (iface.TypeArguments.Length != 2)
            {
                continue;
            }

            var isDecorator = false;
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == "DecoratR.DecoratorAttribute")
                {
                    isDecorator = true;
                    break;
                }
            }

            if (isDecorator)
            {
                return null;
            }

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }
}