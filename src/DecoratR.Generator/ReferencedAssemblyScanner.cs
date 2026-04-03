using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class ReferencedAssemblyScanner
{
    private const string HandlerRegistrationAttributeName = "DecoratRHandlerRegistrationAttribute";
    private const string HandlerServiceTypeAttributeName = "DecoratRHandlerServiceTypeAttribute";

    public static ReferencedRegistrationData Scan(Compilation compilation, CancellationToken cancellationToken)
    {
        var registryClassNames = ImmutableArray.CreateBuilder<string>();
        var serviceTypes = ImmutableArray.CreateBuilder<HandlerMetadata>();

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var attr in referencedAssembly.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;

                if (attrName == HandlerRegistrationAttributeName && attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string className)
                {
                    registryClassNames.Add(className);
                }
                else if (attrName == HandlerServiceTypeAttributeName && attr.ConstructorArguments.Length == 2 && attr.ConstructorArguments[0].Value is string requestType && attr.ConstructorArguments[1].Value is string responseType)
                {
                    serviceTypes.Add(new HandlerMetadata(string.Empty, requestType, responseType));
                }
            }
        }

        return new ReferencedRegistrationData(
            registryClassNames.ToImmutable(),
            serviceTypes.ToImmutable());
    }
}