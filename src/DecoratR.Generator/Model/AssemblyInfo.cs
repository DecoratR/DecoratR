namespace DecoratR.Generator.Model;

/// <summary>
/// The compilation's assembly name and the sanitized namespace generated code is placed in.
/// </summary>
internal sealed record AssemblyInfo(string Name, string Namespace)
{
    public static AssemblyInfo Create(string? assemblyName)
    {
        var name = string.IsNullOrWhiteSpace(assemblyName) ? "DecoratRGenerated" : assemblyName!;
        return new AssemblyInfo(name, IdentifierSanitizer.ToNamespace(name));
    }
}
