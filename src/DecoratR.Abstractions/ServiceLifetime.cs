namespace DecoratR;

/// <summary>
/// Specifies the lifetime of a handler in the dependency injection container.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// A new instance is created every time it is requested.
    /// </summary>
    Transient,

    /// <summary>
    /// A new instance is created once per scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// A single instance is created and shared across all requests.
    /// </summary>
    Singleton
}