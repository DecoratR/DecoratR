using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DecoratR.Generator.Model;

/// <summary>
/// An equatable description of a diagnostic that can flow through the incremental pipeline.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] args) =>
        new(descriptor, location, ImmutableArray.Create(args));

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Length];
        for (var i = 0; i < args.Length; i++) args[i] = MessageArgs[i];

        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, args);
    }
}
