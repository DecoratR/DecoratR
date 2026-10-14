namespace DecoratR.Generator.Model;

/// <summary>
/// The generated handler registry of a referenced assembly.
/// </summary>
/// <param name="Type">The fully qualified (<c>global::</c>-prefixed) registry type name.</param>
/// <param name="HasFactories">
/// <see langword="true"/> when <c>Handlers</c> is a <c>ReadOnlySpan&lt;DecoratR.Metadata.HandlerRegistration&gt;</c>
/// with generated factories; <see langword="false"/> for registries emitted by earlier generator versions, whose
/// nested <c>HandlerRegistration</c> record only carries the service and implementation type.
/// </param>
internal sealed record ReferencedRegistry(string Type, bool HasFactories);

/// <summary>
/// Everything the composition root learns about referenced assemblies that were compiled with
/// <c>[GenerateDecoratRMetadata]</c>.
/// </summary>
internal sealed record ReferencedRegistrationData(
    EquatableArray<ReferencedRegistry> Registries,
    EquatableArray<HandlerMetadata> Handlers,
    EquatableArray<ReferencedDecoratorInfo> Decorators)
{
    public static readonly ReferencedRegistrationData Empty = new(
        EquatableArray<ReferencedRegistry>.Empty,
        EquatableArray<HandlerMetadata>.Empty,
        EquatableArray<ReferencedDecoratorInfo>.Empty);
}
