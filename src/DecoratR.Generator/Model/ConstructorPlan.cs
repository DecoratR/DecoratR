namespace DecoratR.Generator.Model;

/// <summary>Where the generated code takes a constructor argument from.</summary>
internal enum ConstructorArgumentKind
{
    /// <summary><c>GetRequiredService&lt;T&gt;(sp)</c>.</summary>
    Service,

    /// <summary><c>GetRequiredKeyedService&lt;T&gt;(sp, key)</c> for <c>[FromKeyedServices(key)]</c>.</summary>
    KeyedService,

    /// <summary>The <see cref="System.IServiceProvider"/> passed to the factory.</summary>
    ServiceProvider,

    /// <summary>The decorated inner handler (decorators only).</summary>
    InnerHandler,
}

/// <summary>One argument of a constructor call emitted by the generator.</summary>
/// <param name="Type">
/// The fully qualified parameter type. For decorators, the decorator's own type parameters are replaced by the
/// <c>{TRequest}</c>/<c>{TResponse}</c> placeholders of <see cref="Detection.ConstraintSerializer"/>.
/// </param>
/// <param name="Key">The C# literal of the service key for <see cref="ConstructorArgumentKind.KeyedService"/>.</param>
internal readonly record struct ConstructorArgument(ConstructorArgumentKind Kind, string Type, string? Key = null);

/// <summary>
/// The constructor the generated code calls directly instead of going through <c>ActivatorUtilities</c>.
/// </summary>
internal sealed record ConstructorPlan(EquatableArray<ConstructorArgument> Arguments);
