using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class DecoratorDetector
{
    public static DecoratorMetadata? GetMetadata(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol) return null;

        if (symbol.TypeParameters.Length == 0) return null;

        if (symbol.IsAbstract || symbol.IsStatic) return null;

        var order = 0;
        foreach (var attr in context.Attributes)
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                order = orderValue;

        var openGenericName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var angleIndex = openGenericName.IndexOf('<');
        if (angleIndex >= 0) openGenericName = openGenericName.Substring(0, angleIndex);

        // Determine whether this is a stream decorator (IStreamRequestHandler) or regular (IRequestHandler)
        var isStream = false;
        foreach (var iface in symbol.AllInterfaces)
            if (StreamHandlerDetector.IsStreamRequestHandlerInterface(iface))
            {
                isStream = true;
                break;
            }

        // Extract type constraints on TRequest (first type parameter)
        var requestConstraints = ExtractRequestConstraints(symbol.TypeParameters[0]);

        return new DecoratorMetadata(openGenericName, order, requestConstraints, isStream);
    }

    private static EquatableArray<string> ExtractRequestConstraints(ITypeParameterSymbol typeParameter)
    {
        if (typeParameter.ConstraintTypes.Length == 0)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>(typeParameter.ConstraintTypes.Length);

        foreach (var constraint in typeParameter.ConstraintTypes)
            builder.Add(constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return builder.ToImmutable();
    }
}