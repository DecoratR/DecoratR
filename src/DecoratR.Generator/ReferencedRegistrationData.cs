using System.Collections.Immutable;

namespace DecoratR.Generator;

internal readonly struct ReferencedRegistrationData : IEquatable<ReferencedRegistrationData>
{
    public ReferencedRegistrationData(
        ImmutableArray<string> registryClassNames,
        ImmutableArray<HandlerMetadata> serviceTypes,
        ImmutableArray<DecoratorMetadata> decorators)
    {
        RegistryClassNames = registryClassNames;
        ServiceTypes = serviceTypes;
        Decorators = decorators;
    }

    public ImmutableArray<string> RegistryClassNames { get; }

    public ImmutableArray<HandlerMetadata> ServiceTypes { get; }

    public ImmutableArray<DecoratorMetadata> Decorators { get; }

    public bool Equals(ReferencedRegistrationData other)
    {
        if (RegistryClassNames.Length != other.RegistryClassNames.Length ||
            ServiceTypes.Length != other.ServiceTypes.Length ||
            Decorators.Length != other.Decorators.Length)
        {
            return false;
        }

        for (var i = 0; i < RegistryClassNames.Length; i++)
        {
            if (RegistryClassNames[i] != other.RegistryClassNames[i])
            {
                return false;
            }
        }

        for (var i = 0; i < ServiceTypes.Length; i++)
        {
            if (!ServiceTypes[i].Equals(other.ServiceTypes[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < Decorators.Length; i++)
        {
            if (!Decorators[i].Equals(other.Decorators[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is ReferencedRegistrationData other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var name in RegistryClassNames)
            {
                hash = hash * 31 + name.GetHashCode();
            }

            foreach (var s in ServiceTypes)
            {
                hash = hash * 31 + s.GetHashCode();
            }

            foreach (var d in Decorators)
            {
                hash = hash * 31 + d.GetHashCode();
            }

            return hash;
        }
    }
}