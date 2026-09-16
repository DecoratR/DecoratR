namespace DecoratR.Generator.Model;

/// <summary>
/// Everything the composition root learns about referenced assemblies that were compiled with
/// <c>[GenerateDecoratRMetadata]</c>.
/// </summary>
internal sealed record ReferencedRegistrationData(
    EquatableArray<string> RegistryTypes,
    EquatableArray<HandlerMetadata> Handlers,
    EquatableArray<ReferencedDecoratorInfo> Decorators)
{
    public static readonly ReferencedRegistrationData Empty = new(
        EquatableArray<string>.Empty,
        EquatableArray<HandlerMetadata>.Empty,
        EquatableArray<ReferencedDecoratorInfo>.Empty);
}
