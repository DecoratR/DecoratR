using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

#pragma warning disable RS2008

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor NothingFound = new(
        "DCTR001",
        "No handlers or decorators found",
        "Assembly '{0}' has [GenerateDecoratRMetadata] but no handlers or decorators were found",
        "DecoratR.Generator",
        DiagnosticSeverity.Warning,
        true);

    public static readonly DiagnosticDescriptor HandlersDiscovered = new(
        "DCTR002",
        "Handlers discovered",
        "DecoratR source generator discovered {0} handler(s) in assembly '{1}'",
        "DecoratR.Generator",
        DiagnosticSeverity.Info,
        true);

    public static readonly DiagnosticDescriptor DecoratorsDiscovered = new(
        "DCTR003",
        "Decorators discovered",
        "DecoratR source generator discovered {0} decorator(s) in assembly '{1}'",
        "DecoratR.Generator",
        DiagnosticSeverity.Info,
        true);
}