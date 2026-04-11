using System.Collections.Immutable;

namespace DecoratR.Generator;

internal readonly struct ReferencedRegistrationData(
    ImmutableArray<string> registryClassNames,
    ImmutableArray<HandlerMetadata> serviceTypes,
    ImmutableArray<ReferencedDecoratorInfo> decorators,
    ImmutableArray<HandlerMetadata> streamServiceTypes,
    ImmutableArray<ReferencedDecoratorInfo> streamDecorators)
    : IEquatable<ReferencedRegistrationData>
{
    public ImmutableArray<string> RegistryClassNames { get; } = registryClassNames;

    public ImmutableArray<HandlerMetadata> ServiceTypes { get; } = serviceTypes;

    public ImmutableArray<ReferencedDecoratorInfo> Decorators { get; } = decorators;

    public ImmutableArray<HandlerMetadata> StreamServiceTypes { get; } = streamServiceTypes;

    public ImmutableArray<ReferencedDecoratorInfo> StreamDecorators { get; } = streamDecorators;

    public bool Equals(ReferencedRegistrationData other)
    {
        if (RegistryClassNames.Length != other.RegistryClassNames.Length ||
            ServiceTypes.Length != other.ServiceTypes.Length ||
            Decorators.Length != other.Decorators.Length ||
            StreamServiceTypes.Length != other.StreamServiceTypes.Length ||
            StreamDecorators.Length != other.StreamDecorators.Length)
            return false;

        for (var i = 0; i < RegistryClassNames.Length; i++)
            if (RegistryClassNames[i] != other.RegistryClassNames[i])
                return false;

        for (var i = 0; i < ServiceTypes.Length; i++)
            if (!ServiceTypes[i].Equals(other.ServiceTypes[i]))
                return false;

        for (var i = 0; i < Decorators.Length; i++)
            if (!Decorators[i].Equals(other.Decorators[i]))
                return false;

        for (var i = 0; i < StreamServiceTypes.Length; i++)
            if (!StreamServiceTypes[i].Equals(other.StreamServiceTypes[i]))
                return false;

        for (var i = 0; i < StreamDecorators.Length; i++)
            if (!StreamDecorators[i].Equals(other.StreamDecorators[i]))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is ReferencedRegistrationData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var name in RegistryClassNames) hash = hash * 31 + name.GetHashCode();

            foreach (var s in ServiceTypes) hash = hash * 31 + s.GetHashCode();

            foreach (var d in Decorators) hash = hash * 31 + d.GetHashCode();

            foreach (var s in StreamServiceTypes) hash = hash * 31 + s.GetHashCode();

            foreach (var d in StreamDecorators) hash = hash * 31 + d.GetHashCode();

            return hash;
        }
    }
}