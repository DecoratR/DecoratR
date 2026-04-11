using System.Collections.Immutable;
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
            if (!IsRequestHandlerInterface(iface)) continue;

            var isDecorator = false;
            foreach (var attr in symbol.GetAttributes())
                if (attr.AttributeClass is { MetadataName: "DecoratorAttribute", ContainingNamespace: { Name: "DecoratR", ContainingNamespace.IsGlobalNamespace: true } })
                {
                    isDecorator = true;
                    break;
                }

            if (isDecorator) return null;

            var requestType = iface.TypeArguments[0];
            var hierarchy = BuildTypeHierarchy(requestType);

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                hierarchy);
        }

        return null;
    }

    internal static EquatableArray<string> BuildTypeHierarchy(ITypeSymbol typeSymbol)
    {
        // Pre-size: the type itself + all interfaces + estimated base chain depth
        var builder = ImmutableArray.CreateBuilder<string>(1 + typeSymbol.AllInterfaces.Length + 2);

        // The type itself
        builder.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        // All implemented interfaces (transitive)
        foreach (var impl in typeSymbol.AllInterfaces)
            builder.Add(impl.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        // Base type chain (excluding System.Object)
        var baseType = typeSymbol.BaseType;
        while (baseType is not null && baseType.SpecialType != SpecialType.System_Object)
        {
            builder.Add(baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            baseType = baseType.BaseType;
        }

        return builder.ToImmutable();
    }

    private static bool IsRequestHandlerInterface(INamedTypeSymbol iface)
    {
        if (iface.TypeArguments.Length != 2) return false;

        var original = iface.OriginalDefinition;
        if (original.MetadataName != "IRequestHandler`2") return false;

        var ns = original.ContainingNamespace;
        return ns is { Name: "DecoratR", ContainingNamespace.IsGlobalNamespace: true };
    }
}