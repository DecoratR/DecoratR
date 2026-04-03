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
    private const string HandlerRegistrationAttributeName = "DecoratRHandlerRegistrationAttribute";
    private const string HandlerServiceTypeAttributeName = "DecoratRHandlerServiceTypeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Emit the marker attributes
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenerateHandlerRegistrationsAttribute.g.cs",
                SourceGenerationHelper.GenerateHandlerAttribute());
            ctx.AddSource("GenerateDecoratRRegistrationsAttribute.g.cs",
                SourceGenerationHelper.GenerateFullAttribute());
            ctx.AddSource("DecoratRRegistrationMethodAttribute.g.cs",
                SourceGenerationHelper.GenerateRegistrationMethodAttribute());
            ctx.AddSource("DecoratRHandlerRegistrationAttribute.g.cs",
                SourceGenerationHelper.GenerateHandlerRegistrationAttribute());
            ctx.AddSource("DecoratRHandlerServiceTypeAttribute.g.cs",
                SourceGenerationHelper.GenerateHandlerServiceTypeAttribute());
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
            if (!hasAttr)
            {
                return;
            }

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

        // Local decorators (open-generic classes with [Decorator] attribute)
        var localDecorators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DecoratorAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => GetDecoratorMetadata(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var collectedDecorators = localDecorators.Collect();

        // Cross-assembly: discover registration methods and service types via assembly attributes
        var referencedRegistrations = context.CompilationProvider
            .Select(static (compilation, ct) => GetRegistrationDataFromReferencedAssemblies(compilation, ct));

        var fullCombined = hasFullAttribute
            .Combine(collectedLocalHandlers)
            .Combine(referencedRegistrations)
            .Combine(collectedDecorators)
            .Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown"));

        context.RegisterSourceOutput(fullCombined, static (spc, source) =>
        {
            var ((((hasAttr, localH), referenced), decorators), assemblyName) = source;
            if (!hasAttr)
            {
                return;
            }

            var sortedLocalHandlers = localH
                .Distinct()
                .OrderBy(h => h.HandlerFullyQualifiedName)
                .ToList();

            var sortedDecorators = decorators
                .Distinct()
                .OrderBy(d => d.Order)
                .ThenBy(d => d.DecoratorFullyQualifiedName)
                .ToList();

            EmitFullRegistrations(spc, assemblyName, sortedLocalHandlers, referenced.Methods, referenced.ServiceTypes, sortedDecorators);
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
        var classDeclaration = (ClassDeclarationSyntax) context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)
            is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // Skip abstract, static, and open generic types
        if (symbol.IsAbstract || symbol.IsStatic || symbol.TypeParameters.Length > 0)
        {
            return null;
        }

        return ExtractHandlerMetadata(symbol);
    }

    private static HandlerMetadata? ExtractHandlerMetadata(INamedTypeSymbol symbol)
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

            // Exclude decorators: classes annotated with [Decorator]
            var isDecorator = symbol.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "DecoratR.DecoratorAttribute");

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

    // ─── Cross-assembly registration discovery via assembly attributes ───────

    private static ReferencedRegistrationData GetRegistrationDataFromReferencedAssemblies(
        Compilation compilation, CancellationToken cancellationToken)
    {
        var methods = new List<RegistrationMethodMetadata>();
        var serviceTypes = new List<HandlerMetadata>();

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var attr in referencedAssembly.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;

                if (attrName == HandlerRegistrationAttributeName && attr.ConstructorArguments.Length == 2 && attr.ConstructorArguments[0].Value is string className && attr.ConstructorArguments[1].Value is string methodName)
                {
                    methods.Add(new RegistrationMethodMetadata(className, methodName));
                }
                else if (attrName == HandlerServiceTypeAttributeName && attr.ConstructorArguments.Length == 2 && attr.ConstructorArguments[0].Value is string requestType && attr.ConstructorArguments[1].Value is string responseType)
                {
                    serviceTypes.Add(new HandlerMetadata(string.Empty, requestType, responseType));
                }
            }
        }

        return new ReferencedRegistrationData(methods, serviceTypes);
    }

    // ─── Decorator detection ──────────────────────────────────────────────────

    private static DecoratorMetadata? GetDecoratorMetadata(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // Decorators must be open generic
        if (symbol.TypeParameters.Length == 0)
        {
            return null;
        }

        if (symbol.IsAbstract || symbol.IsStatic)
        {
            return null;
        }

        // Extract Order from [Decorator] attribute
        var order = 0;
        foreach (var attr in context.Attributes)
        {
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                {
                    order = orderValue;
                }
            }
        }

        // Store as open generic form (without type args)
        var openGenericName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var angleIndex = openGenericName.IndexOf('<');
        if (angleIndex >= 0)
        {
            openGenericName = openGenericName.Substring(0, angleIndex);
        }

        return new DecoratorMetadata(openGenericName, order);
    }

    // ─── Emission helpers ─────────────────────────────────────────────────────

    private static void EmitHandlerRegistry(
        SourceProductionContext spc,
        string assemblyName,
        ImmutableArray<HandlerMetadata> handlers)
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
        IReadOnlyList<HandlerMetadata> localHandlers,
        IReadOnlyList<RegistrationMethodMetadata> referencedMethods,
        IReadOnlyList<HandlerMetadata> referencedServiceTypes,
        IReadOnlyList<DecoratorMetadata> decorators)
    {
        var totalHandlerCount = localHandlers.Count + referencedServiceTypes.Count;

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

        if (decorators.Count > 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DecoratorsDiscovered, Location.None, decorators.Count, assemblyName));
        }

        spc.AddSource("DecoratRRegistrations.g.cs",
            SourceGenerationHelper.GenerateFullRegistrations(assemblyName, localHandlers, referencedMethods, referencedServiceTypes, decorators));
    }
}

/// <summary>
/// Holds cross-assembly registration data discovered from assembly-level attributes.
/// </summary>
internal sealed class ReferencedRegistrationData : IEquatable<ReferencedRegistrationData>
{
    public ReferencedRegistrationData(
        IReadOnlyList<RegistrationMethodMetadata> methods,
        IReadOnlyList<HandlerMetadata> serviceTypes)
    {
        Methods = methods;
        ServiceTypes = serviceTypes;
    }

    public IReadOnlyList<RegistrationMethodMetadata> Methods { get; }

    public IReadOnlyList<HandlerMetadata> ServiceTypes { get; }

    public bool Equals(ReferencedRegistrationData? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Methods.SequenceEqual(other.Methods) && ServiceTypes.SequenceEqual(other.ServiceTypes);
    }

    public override bool Equals(object? obj) => Equals(obj as ReferencedRegistrationData);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var m in Methods)
            {
                hash = hash * 31 + m.GetHashCode();
            }

            foreach (var s in ServiceTypes)
            {
                hash = hash * 31 + s.GetHashCode();
            }

            return hash;
        }
    }
}