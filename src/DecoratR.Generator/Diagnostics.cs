using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor NoHandlersFound = new(
        id: "DCTR001",
        title: "No handlers found",
        messageFormat: "Assembly '{0}' has [GenerateHandlerRegistrations] but no IRequestHandler<,> implementations were found",
        category: "DecoratR.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HandlersDiscovered = new(
        id: "DCTR002",
        title: "Handlers discovered",
        messageFormat: "DecoratR source generator discovered {0} handler(s) in assembly '{1}'",
        category: "DecoratR.Generator",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
