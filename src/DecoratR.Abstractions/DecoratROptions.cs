using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

/// <summary>
/// Configuration options for the generated <c>AddDecoratR()</c> registration method.
/// </summary>
public sealed class DecoratROptions
{
    /// <summary>
    /// The service lifetime used for handler registrations. Decorators inherit the lifetime of the handler
    /// they wrap. Default is <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
}
