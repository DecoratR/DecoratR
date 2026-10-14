using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace DecoratR.Metadata;

/// <summary>
/// Infrastructure type used by the DecoratR source generator. Describes one handler registration in the generated
/// handler registry of an assembly, so that a composition root can register (possibly internal) handlers of that
/// assembly without referencing their types.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct HandlerRegistration
{
    /// <summary>Creates a registration that is resolved through a generated factory.</summary>
    /// <param name="serviceType">The service type the handler is registered as.</param>
    /// <param name="implementationType">The handler implementation type.</param>
    /// <param name="factory">
    /// Creates the handler, or <see langword="null"/> when the generator cannot call the handler's constructor
    /// directly and the handler is registered by <paramref name="implementationType"/> instead.
    /// </param>
    public HandlerRegistration(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
        Func<IServiceProvider, object>? factory = null)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Factory = factory;
    }

    /// <summary>The service type the handler is registered as.</summary>
    public Type ServiceType { get; }

    /// <summary>
    /// The handler implementation type. Used for the validation registration (see
    /// <see cref="DecoratRServiceKeys.Validation"/>) and, without a <see cref="Factory"/>, for the registration itself.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ImplementationType { get; }

    /// <summary>
    /// The generated factory that constructs the handler, or <see langword="null"/> when the handler is registered by
    /// <see cref="ImplementationType"/>.
    /// </summary>
    public Func<IServiceProvider, object>? Factory { get; }
}
