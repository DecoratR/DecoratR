using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class ReferencedAssemblyScanner
{
    private const string HandlerRegistrationAttributeName = "DecoratRHandlerRegistrationAttribute";
    private const string HandlerServiceTypeAttributeName = "DecoratRHandlerServiceTypeAttribute";
    private const string DecoratorRegistrationAttributeName = "DecoratRDecoratorRegistrationAttribute";

    public static ReferencedRegistrationData Scan(Compilation compilation, CancellationToken cancellationToken)
    {
        var registryClassNames = ImmutableArray.CreateBuilder<string>();
        var serviceTypes = ImmutableArray.CreateBuilder<HandlerMetadata>();
        var decorators = ImmutableArray.CreateBuilder<ReferencedDecoratorInfo>();

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var attr in referencedAssembly.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;

                if (attrName == HandlerRegistrationAttributeName && attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is string className)
                {
                    registryClassNames.Add(className);
                }
                else if (attrName == HandlerServiceTypeAttributeName && attr.ConstructorArguments.Length == 2 &&
                         attr.ConstructorArguments[0].Value is string requestType &&
                         attr.ConstructorArguments[1].Value is string responseType)
                {
                    var hierarchy = ReadNamedStringArgument(attr, "RequestTypeHierarchy");
                    var hierarchyArray = ParseSemicolonDelimited(hierarchy);
                    serviceTypes.Add(new HandlerMetadata(string.Empty, requestType, responseType, hierarchyArray));
                }
                else if (attrName == DecoratorRegistrationAttributeName && attr.ConstructorArguments.Length == 2 &&
                         attr.ConstructorArguments[0].Value is string applyMethodName &&
                         attr.ConstructorArguments[1].Value is int order)
                {
                    var constraintStr = ReadNamedStringArgument(attr, "RequestConstraintTypes");
                    var constraintArray = ParseSemicolonDelimited(constraintStr);
                    decorators.Add(new ReferencedDecoratorInfo(applyMethodName, order, constraintArray));
                }
            }
        }

        return new ReferencedRegistrationData(
            registryClassNames.ToImmutable(),
            serviceTypes.ToImmutable(),
            decorators.ToImmutable());
    }

    private static string ReadNamedStringArgument(AttributeData attr, string name)
    {
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == name && namedArg.Value.Value is string value)
                return value;

        return "";
    }

    private static EquatableArray<string> ParseSemicolonDelimited(string value)
    {
        if (string.IsNullOrEmpty(value))
            return ImmutableArray<string>.Empty;

        var parts = value.Split(';');
        var builder = ImmutableArray.CreateBuilder<string>(parts.Length);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                builder.Add(trimmed);
        }

        return builder.ToImmutable();
    }
}