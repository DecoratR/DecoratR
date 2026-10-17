using System.ComponentModel;

namespace DecoratR.Metadata;

/// <summary>
/// Infrastructure type used by the DecoratR source generator: service keys of the registrations that the generated
/// <c>AddDecoratR()</c> adds besides the handlers themselves.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DecoratRServiceKeys
{
    /// <summary>
    /// Key of the validation registrations. <c>AddDecoratR()</c> registers every handler implementation type under
    /// this key in addition to its (decorated) factory registration. These registrations are never resolved; they
    /// exist so that <c>ServiceProviderOptions.ValidateOnBuild</c> and <c>ValidateScopes</c> check the constructor
    /// dependencies of handlers, which the container cannot see through a factory registration.
    /// </summary>
    public const string Validation = "DecoratR.Validation";
}
