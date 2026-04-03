using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class DecoratorDetector
{
    public static DecoratorMetadata? GetMetadata(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (symbol.TypeParameters.Length == 0)
        {
            return null;
        }

        if (symbol.IsAbstract || symbol.IsStatic)
        {
            return null;
        }

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

        var openGenericName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var angleIndex = openGenericName.IndexOf('<');
        if (angleIndex >= 0)
        {
            openGenericName = openGenericName.Substring(0, angleIndex);
        }

        return new DecoratorMetadata(openGenericName, order);
    }
}
