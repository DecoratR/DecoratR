namespace DecoratR.Generator;

/// <summary>
/// Metadata names and fully qualified display names of the types the generator inspects or emits.
/// </summary>
internal static class WellKnownTypes
{
    public const string RootNamespace = "DecoratR";
    public const string MetadataNamespace = "Metadata";

    public const string DecoratorAttributeMetadataName = "DecoratR.DecoratorAttribute";
    public const string DecoratorAttributeName = "DecoratorAttribute";
    public const string GenerateMetadataAttributeMetadataName = "DecoratR.GenerateDecoratRMetadataAttribute";
    public const string GenerateMetadataAttributeShortName = "GenerateDecoratRMetadata";
    public const string GenerateRegistrationsAttributeMetadataName = "DecoratR.GenerateDecoratRRegistrationsAttribute";
    public const string GenerateRegistrationsAttributeShortName = "GenerateDecoratRRegistrations";

    public const string RequestHandlerMetadataName = "IRequestHandler`2";
    public const string VoidRequestHandlerMetadataName = "IRequestHandler`1";
    public const string StreamRequestHandlerMetadataName = "IStreamRequestHandler`2";
    public const string UnitName = "Unit";
    public const string RequestMarkerName = "IRequest";
    public const string StreamRequestMarkerName = "IStreamRequest";

    public const string RegistryAttributeName = "DecoratRRegistryAttribute";
    public const string HandlerAttributeName = "DecoratRHandlerAttribute";
    public const string DecoratorMetadataAttributeName = "DecoratRDecoratorAttribute";

    public const string ServiceCollectionMetadataName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    // Fully qualified names used in emitted code.
    public const string RequestMarker = "global::DecoratR.IRequest";
    public const string StreamRequestMarker = "global::DecoratR.IStreamRequest";
    public const string RequestHandler = "global::DecoratR.IRequestHandler";
    public const string StreamRequestHandler = "global::DecoratR.IStreamRequestHandler";
    public const string VoidRequestHandler = "global::DecoratR.VoidRequestHandler";
    public const string Options = "global::DecoratR.DecoratROptions";
    public const string RegistryAttribute = "global::DecoratR.Metadata.DecoratRRegistry";
    public const string HandlerAttribute = "global::DecoratR.Metadata.DecoratRHandler";
    public const string DecoratorMetadataAttribute = "global::DecoratR.Metadata.DecoratRDecorator";

    public const string ServiceCollection = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";
    public const string ServiceDescriptor = "global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor";
    public const string ActivatorUtilities = "global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities";
    public const string ServiceProvider = "global::System.IServiceProvider";
    public const string Type = "global::System.Type";
    public const string Func = "global::System.Func";
    public const string Action = "global::System.Action";
    public const string InvalidOperationException = "global::System.InvalidOperationException";

    public const string GeneratedCodeAttribute = "global::System.CodeDom.Compiler.GeneratedCode";
    public const string EditorBrowsableNever =
        "global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)";
    public const string DynamicallyAccessedPublicConstructors =
        "global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)";

    public const string HandlerRegistryClassName = "DecoratRHandlerRegistry";
    public const string DecoratorRegistryClassName = "DecoratRDecoratorRegistry";
    public const string ExtensionsClassName = "DecoratRServiceCollectionExtensions";
    public const string ExtensionsNamespace = "Microsoft.Extensions.DependencyInjection";

    public static string HandlerInterface(bool isStream) => isStream ? StreamRequestHandler : RequestHandler;

    public static string RequestMarkerFor(bool isStream) => isStream ? StreamRequestMarker : RequestMarker;
}
