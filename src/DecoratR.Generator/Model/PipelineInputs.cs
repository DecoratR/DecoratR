namespace DecoratR.Generator.Model;

/// <summary>Input of the metadata (library) output step.</summary>
internal sealed record MetadataInput(
    GenerationTrigger Trigger,
    HandlerSet Handlers,
    DecoratorSet Decorators,
    AssemblyInfo Assembly,
    bool HasServiceCollection);

/// <summary>Input of the registrations (composition root) output step.</summary>
internal sealed record RegistrationsInput(
    GenerationTrigger Trigger,
    HandlerSet Handlers,
    DecoratorSet Decorators,
    ReferencedRegistrationData Referenced,
    AssemblyInfo Assembly,
    bool HasServiceCollection);

/// <summary>Input of the detection-diagnostics output step.</summary>
internal sealed record DetectionDiagnosticsInput(
    EquatableArray<DiagnosticInfo> HandlerDiagnostics,
    EquatableArray<DiagnosticInfo> DecoratorDiagnostics,
    bool AnyTriggerPresent);
