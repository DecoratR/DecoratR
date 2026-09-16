using System.Collections.Immutable;
using DecoratR.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Discovers <c>IRequestHandler&lt;,&gt;</c> and <c>IStreamRequestHandler&lt;,&gt;</c> implementations.
/// </summary>
internal static class HandlerDetector
{
    /// <summary>
    /// Cheap syntactic pre-filter: only non-abstract, non-static, non-generic classes, records and structs with a
    /// base list can be handlers.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node)
    {
        if (node is not (ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax)) return false;

        var declaration = (TypeDeclarationSyntax)node;
        if (declaration.BaseList is not { Types.Count: > 0 }) return false;
        if (declaration.TypeParameterList is not null) return false;

        foreach (var modifier in declaration.Modifiers)
            if (modifier.IsKind(SyntaxKind.AbstractKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword))
                return false;

        return true;
    }

    public static HandlerDetectionResult? Detect(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract || symbol.IsStatic || symbol.IsGenericType || symbol.IsNestedInGenericType())
            return null;

        // Decorators are discovered by DecoratorDetector.
        if (symbol.HasDecoratorAttribute()) return null;

        // Partial types: only the first declaration that carries a base list produces metadata.
        if (!IsPrimaryDeclaration(symbol, declaration, cancellationToken)) return null;

        var interfaces = ImmutableArray.CreateBuilder<(INamedTypeSymbol Interface, bool IsStream)>();
        foreach (var iface in symbol.AllInterfaces)
            if (iface.IsHandlerInterface(out var isStream))
                interfaces.Add((iface, isStream));

        if (interfaces.Count == 0) return null;

        var handlerType = symbol.ToFullyQualifiedName();
        var location = LocationInfo.From(symbol);

        if (symbol.TypeKind != TypeKind.Class)
            return new HandlerDetectionResult(
                EquatableArray<HandlerMetadata>.Empty,
                ImmutableArray.Create(DiagnosticInfo.Create(Diagnostics.HandlerIsValueType, location, handlerType)));

        if (!symbol.IsAccessibleFromGeneratedCode())
            return new HandlerDetectionResult(
                EquatableArray<HandlerMetadata>.Empty,
                ImmutableArray.Create(DiagnosticInfo.Create(Diagnostics.TypeNotAccessible, location, handlerType)));

        var handlers = ImmutableArray.CreateBuilder<HandlerMetadata>(interfaces.Count);
        foreach (var (iface, isStream) in interfaces)
            handlers.Add(new HandlerMetadata(handlerType, ServiceTypeInfo.Create(iface, isStream, context.SemanticModel.Compilation), location));

        return new HandlerDetectionResult(handlers.ToImmutable(), EquatableArray<DiagnosticInfo>.Empty);
    }

    /// <summary>Flattens, deduplicates (partial declarations) and sorts all detection results.</summary>
    public static HandlerSet Aggregate(ImmutableArray<HandlerDetectionResult?> results)
    {
        var handlers = new List<HandlerMetadata>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        foreach (var result in results)
        {
            if (result is null) continue;

            foreach (var handler in result.Handlers)
            {
                var duplicate = false;
                foreach (var existing in handlers)
                {
                    if (existing.HandlerType != handler.HandlerType ||
                        !ServiceTypeInfo.SameService(existing.ServiceType, handler.ServiceType))
                        continue;

                    duplicate = true;
                    break;
                }

                if (!duplicate) handlers.Add(handler);
            }

            diagnostics.AddRange(result.Diagnostics);
        }

        handlers.Sort(HandlerMetadata.Compare);

        return new HandlerSet(handlers.ToImmutableArray(), diagnostics.ToImmutable());
    }

    private static bool IsPrimaryDeclaration(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        if (symbol.DeclaringSyntaxReferences.Length == 1) return true;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax { BaseList.Types.Count: > 0 } candidate)
                continue;

            return candidate.SyntaxTree == declaration.SyntaxTree && candidate.Span == declaration.Span;
        }

        return false;
    }
}
