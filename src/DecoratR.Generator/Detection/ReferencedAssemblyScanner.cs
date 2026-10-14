using System.Collections.Immutable;
using DecoratR.Generator.Model;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Detection;

/// <summary>
/// Reads the <c>DecoratR.Metadata</c> assembly attributes of all referenced assemblies.
/// </summary>
internal static class ReferencedAssemblyScanner
{
    private const string AbstractionsAssemblyName = "DecoratR.Abstractions";

    public static ReferencedRegistrationData Scan(Compilation compilation, CancellationToken cancellationToken)
    {
        var registries = new List<string>();
        var handlers = new List<HandlerMetadata>();
        var decorators = new List<ReferencedDecoratorInfo>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only assemblies that reference DecoratR.Abstractions can carry DecoratR metadata; this avoids
            // decoding the attributes of the whole framework on every compilation change.
            if (!ReferencesAbstractions(assembly)) continue;

            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass ||
                    !attributeClass.ContainingNamespace.IsDecoratRMetadataNamespace())
                    continue;

                switch (attributeClass.Name)
                {
                    case WellKnownTypes.RegistryAttributeName:
                        if (attribute.ConstructorArguments.Length == 1 &&
                            attribute.ConstructorArguments[0].Value is string registry)
                            registries.Add(registry);
                        break;

                    case WellKnownTypes.HandlerAttributeName:
                        if (TryReadHandler(attribute, out var handler)) handlers.Add(handler);
                        break;

                    case WellKnownTypes.DecoratorMetadataAttributeName:
                        if (TryReadDecorator(attribute, out var decorator)) decorators.Add(decorator);
                        break;
                }
            }
        }

        registries.Sort(string.CompareOrdinal);
        handlers.Sort(HandlerMetadata.Compare);
        decorators.Sort(ReferencedDecoratorInfo.Compare);

        return new ReferencedRegistrationData(
            registries.ToImmutableArray(),
            handlers.ToImmutableArray(),
            decorators.ToImmutableArray());
    }

    private static bool ReferencesAbstractions(IAssemblySymbol assembly)
    {
        foreach (var module in assembly.Modules)
            foreach (var reference in module.ReferencedAssemblies)
                if (reference.Name == AbstractionsAssemblyName)
                    return true;

        return false;
    }

    private static bool TryReadHandler(AttributeData attribute, out HandlerMetadata handler)
    {
        handler = null!;
        var args = attribute.ConstructorArguments;
        if (args.Length != 4 ||
            args[0].Value is not string handlerType ||
            args[1].Value is not string requestType ||
            args[2].Value is not string responseType ||
            args[3].Value is not bool isStream)
            return false;

        var service = new ServiceTypeInfo(
            requestType,
            responseType,
            isStream,
            ConstraintSerializer.Split(ReadString(attribute, "RequestTypeHierarchy")),
            ConstraintSerializer.Split(ReadString(attribute, "ResponseTypeHierarchy")),
            ReadBool(attribute, "IsPubliclyAccessible", defaultValue: true));

        handler = new HandlerMetadata(handlerType, service, Location: null);
        return true;
    }

    private static bool TryReadDecorator(AttributeData attribute, out ReferencedDecoratorInfo decorator)
    {
        decorator = null!;
        var args = attribute.ConstructorArguments;
        if (args.Length != 4 ||
            args[0].Value is not string applyMethod ||
            args[1].Value is not string decoratorType ||
            args[2].Value is not int order ||
            args[3].Value is not bool isStream)
            return false;

        decorator = new ReferencedDecoratorInfo(
            applyMethod,
            decoratorType,
            order,
            isStream,
            ConstraintSerializer.Split(ReadString(attribute, "RequestConstraints")),
            ConstraintSerializer.Split(ReadString(attribute, "ResponseConstraints")));
        return true;
    }

    private static string? ReadString(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
            if (named.Key == name && named.Value.Value is string value)
                return value;

        return null;
    }

    private static bool ReadBool(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var named in attribute.NamedArguments)
            if (named.Key == name && named.Value.Value is bool value)
                return value;

        return defaultValue;
    }
}
