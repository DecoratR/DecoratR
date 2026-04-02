using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DecoratR.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class DecoratRIncrementalGenerator : IIncrementalGenerator
{
    private const string RequestHandlerMetadataName = "DecoratR.IRequestHandler`2";
    private const string DecoratorMetadataName = "DecoratR.IDecorator`2";
    private const string HandlerAttributeMetadataName = "DecoratR.GenerateHandlerRegistrationsAttribute";
    private const string FullAttributeMetadataName = "DecoratR.GenerateDecoratRRegistrationsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Emit the marker attributes
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenerateHandlerRegistrationsAttribute.g.cs",
                SourceGenerationHelper.GenerateHandlerAttribute());
            ctx.AddSource("GenerateDecoratRRegistrationsAttribute.g.cs",
                SourceGenerationHelper.GenerateFullAttribute());
        });

        // ── Handler-only path ([GenerateHandlerRegistrations]) ──────────────

        var hasHandlerAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                HandlerAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        var localHandlers = BuildLocalHandlerProvider(context);

        var collectedLocalHandlers = localHandlers.Collect();

        var handlerOnlyCombined = hasHandlerAttribute
            .Combine(collectedLocalHandlers)
            .Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown"));

        context.RegisterSourceOutput(handlerOnlyCombined, static (spc, source) =>
        {
            var ((hasAttr, handlers), assemblyName) = source;
            if (!hasAttr) return;
            EmitHandlerRegistry(spc, assemblyName, handlers);
        });

        // ── Full path ([GenerateDecoratRRegistrations]) ──────────────────────

        var hasFullAttribute = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FullAttributeMetadataName,
                static (_, _) => true,
                static (_, _) => true)
            .Collect()
            .Select(static (items, _) => items.Length > 0);

        // Local decorators (open-generic IDecorator<,> implementations)
        var localDecorators = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => GetDecoratorMetadata(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var collectedDecorators = localDecorators.Collect();

        // Cross-assembly handlers from referenced assemblies
        var referencedHandlers = context.CompilationProvider
            .Select(static (compilation, ct) => GetHandlersFromReferencedAssemblies(compilation, ct));

        var fullCombined = hasFullAttribute
            .Combine(collectedLocalHandlers)
            .Combine(referencedHandlers)
            .Combine(collectedDecorators)
            .Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown"));

        context.RegisterSourceOutput(fullCombined, static (spc, source) =>
        {
            var ((((hasAttr, localH), referencedH), decorators), assemblyName) = source;
            if (!hasAttr) return;

            // Merge local + referenced handlers, deduplicate
            var allHandlers = localH
                .Concat(referencedH)
                .Distinct()
                .OrderBy(h => h.HandlerFullyQualifiedName)
                .ToList();

            var sortedDecorators = decorators
                .Distinct()
                .OrderBy(d => d.DecoratorFullyQualifiedName)
                .ToList();

            EmitFullRegistrations(spc, assemblyName, allHandlers, sortedDecorators);
        });
    }

    // ─── Handler detection ────────────────────────────────────────────────────

    private static IncrementalValuesProvider<HandlerMetadata> BuildLocalHandlerProvider(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => GetHandlerMetadata(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);
    }

    private static HandlerMetadata? GetHandlerMetadata(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // Skip abstract, static, and open generic types
        if (symbol.IsAbstract || symbol.IsStatic || symbol.TypeParameters.Length > 0)
            return null;

        return ExtractHandlerMetadata(symbol);
    }

    private static HandlerMetadata? ExtractHandlerMetadata(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() != "DecoratR.IRequestHandler<TRequest, TResponse>")
                continue;

            if (iface.TypeArguments.Length != 2)
                continue;

            // Exclude decorators: they implement IDecorator<,> which extends IRequestHandler<,>
            var isDecorator = symbol.AllInterfaces.Any(i =>
                i.OriginalDefinition.ToDisplayString() == "DecoratR.IDecorator<TRequest, TResponse>");

            if (isDecorator)
                return null;

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }

    // ─── Cross-assembly handler scan ─────────────────────────────────────────

    private static IReadOnlyList<HandlerMetadata> GetHandlersFromReferencedAssemblies(
        Compilation compilation, CancellationToken cancellationToken)
    {
        var requestHandlerSymbol = compilation.GetTypeByMetadataName(RequestHandlerMetadataName);
        var decoratorSymbol = compilation.GetTypeByMetadataName(DecoratorMetadataName);

        if (requestHandlerSymbol is null)
            return [];

        var results = new List<HandlerMetadata>();

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectHandlersFromNamespace(referencedAssembly.GlobalNamespace, requestHandlerSymbol, decoratorSymbol, results, cancellationToken);
        }

        return results;
    }

    private static void CollectHandlersFromNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol requestHandlerSymbol,
        INamedTypeSymbol? decoratorSymbol,
        List<HandlerMetadata> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var type in ns.GetTypeMembers())
        {
            if (type.IsAbstract || type.IsStatic || type.TypeParameters.Length > 0)
                continue;

            if (type.TypeKind != TypeKind.Class)
                continue;

            // Only public types are accessible from the consuming (composition root) assembly
            if (type.DeclaredAccessibility != Accessibility.Public)
                continue;

            var metadata = ExtractHandlerMetadataFromSymbol(type, requestHandlerSymbol, decoratorSymbol);
            if (metadata is not null)
                results.Add(metadata);
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            CollectHandlersFromNamespace(childNs, requestHandlerSymbol, decoratorSymbol, results, cancellationToken);
        }
    }

    private static HandlerMetadata? ExtractHandlerMetadataFromSymbol(
        INamedTypeSymbol symbol,
        INamedTypeSymbol requestHandlerSymbol,
        INamedTypeSymbol? decoratorSymbol)
    {
        var isDecorator = decoratorSymbol is not null &&
            symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, decoratorSymbol));

        if (isDecorator)
            return null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, requestHandlerSymbol))
                continue;

            if (iface.TypeArguments.Length != 2)
                continue;

            return new HandlerMetadata(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }

    // ─── Decorator detection ──────────────────────────────────────────────────

    private static DecoratorMetadata? GetDecoratorMetadata(
        GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // Decorators must be open generic
        if (symbol.TypeParameters.Length == 0)
            return null;

        if (symbol.IsAbstract || symbol.IsStatic)
            return null;

        // Must implement IDecorator<,>
        var implementsDecorator = symbol.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString() == "DecoratR.IDecorator<TRequest, TResponse>");

        if (!implementsDecorator)
            return null;

        // Store as open generic (without type args) so the generator can close it per handler
        var openGenericName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Remove the type arguments from the display string to get the open generic form
        // e.g. "global::MyApp.RequestLoggingDecorator<TRequest, TResponse>" -> "global::MyApp.RequestLoggingDecorator"
        var backtickIndex = openGenericName.IndexOf('<');
        if (backtickIndex >= 0)
            openGenericName = openGenericName.Substring(0, backtickIndex);

        return new DecoratorMetadata(openGenericName);
    }

    // ─── Emission helpers ─────────────────────────────────────────────────────

    private static void EmitHandlerRegistry(
        SourceProductionContext spc,
        string assemblyName,
        System.Collections.Immutable.ImmutableArray<HandlerMetadata> handlers)
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
            SourceGenerationHelper.GenerateHandlerRegistry(assemblyName, sorted));
    }

    private static void EmitFullRegistrations(
        SourceProductionContext spc,
        string assemblyName,
        IReadOnlyList<HandlerMetadata> handlers,
        IReadOnlyList<DecoratorMetadata> decorators)
    {
        if (handlers.Count == 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NoHandlersFound, Location.None, assemblyName));
        }
        else
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.HandlersDiscovered, Location.None, handlers.Count, assemblyName));
        }

        if (decorators.Count > 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DecoratorsDiscovered, Location.None, decorators.Count, assemblyName));
        }

        spc.AddSource("DecoratRRegistrations.g.cs",
            SourceGenerationHelper.GenerateFullRegistrations(assemblyName, handlers, decorators));
    }
}
