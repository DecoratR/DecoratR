namespace DecoratR;

/// <summary>
/// Generates handler and decorator metadata for the current assembly.
/// Apply this to a class library that defines request handlers or decorators but does not own
/// the final <c>IServiceCollection</c> registration call.
/// </summary>
/// <remarks>
/// DecoratR emits a handler registry and a decorator registry plus assembly-level metadata that a
/// composition root marked with <see cref="GenerateDecoratRRegistrationsAttribute"/> consumes
/// across assembly boundaries.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GenerateDecoratRMetadataAttribute : Attribute;
