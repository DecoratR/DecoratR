namespace DecoratR.Generator.Model;

/// <summary>Handlers found on one type declaration plus the diagnostics produced while inspecting it.</summary>
internal sealed record HandlerDetectionResult(
    EquatableArray<HandlerMetadata> Handlers,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>The decorator found on one <c>[Decorator]</c> type plus the diagnostics produced while inspecting it.</summary>
internal sealed record DecoratorDetectionResult(
    DecoratorMetadata? Decorator,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>All local handlers, deduplicated and sorted, plus detection diagnostics.</summary>
internal sealed record HandlerSet(
    EquatableArray<HandlerMetadata> Handlers,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>All local decorators, sorted by order and name, plus detection diagnostics.</summary>
internal sealed record DecoratorSet(
    EquatableArray<DecoratorMetadata> Decorators,
    EquatableArray<DiagnosticInfo> Diagnostics);
