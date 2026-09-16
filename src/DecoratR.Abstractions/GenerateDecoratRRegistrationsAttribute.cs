namespace DecoratR;

/// <summary>
/// Generates the <c>AddDecoratR()</c> registration entry point for the current composition root.
/// </summary>
/// <remarks>
/// Apply this to the host or startup assembly. DecoratR scans the current compilation and referenced
/// assemblies for handler metadata, then emits an <c>IServiceCollection</c> extension method in the
/// <c>Microsoft.Extensions.DependencyInjection</c> namespace that registers handlers and applies decorators
/// in pipeline order.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GenerateDecoratRRegistrationsAttribute : Attribute;
