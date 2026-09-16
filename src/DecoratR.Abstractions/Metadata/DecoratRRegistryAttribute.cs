using System.ComponentModel;

namespace DecoratR.Metadata;

/// <summary>
/// Infrastructure attribute emitted by the DecoratR source generator. Records the fully qualified name of the
/// generated handler registry of an assembly so that a composition root can register its handlers without
/// referencing the (possibly internal) handler types directly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DecoratRRegistryAttribute(string registryType) : Attribute
{
    /// <summary>
    /// The fully qualified (<c>global::</c>-prefixed) name of the generated registry class.
    /// </summary>
    public string RegistryType { get; } = registryType;
}
