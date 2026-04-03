using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void Decorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator, TRequest, TResponse>()
            where TService : IRequestHandler<TRequest, TResponse>
            where TDecorator : TService
            where TRequest : IRequest
        {
            services.Decorate(typeof(TService), typeof(TDecorator));
        }

        private void Decorate(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType)
        {
            var wrappedDescriptor = services.FirstOrDefault(s => s.ServiceType == serviceType) ??
                                    throw new InvalidOperationException(
                                        $"No service of type {serviceType.FullName} has been registered.");

            var index = services.IndexOf(wrappedDescriptor);
            services.RemoveAt(index);

            services.Insert(index, ServiceDescriptor.Describe(
                serviceType,
                provider =>
                {
                    var innerInstance = wrappedDescriptor.ImplementationFactory is not null
                        ? wrappedDescriptor.ImplementationFactory(provider)
                        : wrappedDescriptor.ImplementationInstance ?? ActivatorUtilities.CreateInstance(provider, wrappedDescriptor.ImplementationType!);

                    return ActivatorUtilities.CreateInstance(provider, decoratorType, innerInstance);
                },
                wrappedDescriptor.Lifetime));
        }
    }
}