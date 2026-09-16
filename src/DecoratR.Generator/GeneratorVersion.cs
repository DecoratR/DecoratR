namespace DecoratR.Generator;

internal static class GeneratorVersion
{
    /// <summary>
    /// The generator's assembly version (set through <c>/p:Version</c> when packing). Emitted into
    /// <c>[GeneratedCode]</c> attributes.
    /// </summary>
    public static readonly string Value =
        typeof(GeneratorVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
