using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

internal static class ServiceCollectionExtensions
{
    internal static void Decorate(
        this IServiceCollection services,
        Type serviceType, Type decoratorType)
    {
        var wrappedDescriptor = services.FirstOrDefault(s => s.ServiceType == serviceType)
            ?? throw new InvalidOperationException(
                $"No service of type {serviceType.FullName} has been registered.");

        var index = services.IndexOf(wrappedDescriptor);
        services.RemoveAt(index);

        services.Insert(index, ServiceDescriptor.Describe(
            serviceType,
            provider =>
            {
                var innerInstance = wrappedDescriptor.ImplementationFactory is not null
                    ? wrappedDescriptor.ImplementationFactory(provider)
                    : wrappedDescriptor.ImplementationInstance
                      ?? ActivatorUtilities.CreateInstance(provider, wrappedDescriptor.ImplementationType!);

                return ActivatorUtilities.CreateInstance(provider, decoratorType, innerInstance);
            },
            wrappedDescriptor.Lifetime));
    }
}
