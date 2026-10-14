using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Runtime.Benchmarks;

/// <summary>Request type of the startup scenario; every <typeparamref name="TKey"/> yields a distinct service type.</summary>
public sealed record KeyedRequest<TKey> : IRequest;

/// <summary>
/// Handler of the startup scenario. Generic, so the generator ignores it; the scenario registers it itself, exactly
/// like the generated <c>AddDecoratR()</c> registers the handlers of a referenced module registry.
/// </summary>
public sealed class KeyedHandler<TKey>(Repository repository) : IRequestHandler<KeyedRequest<TKey>, int>
{
    public Repository Repository { get; } = repository;

    public ValueTask<int> HandleAsync(KeyedRequest<TKey> request, CancellationToken cancellationToken = default) => new(0);
}

public sealed class Left<T>;

public sealed class Right<T>;

public sealed class Root;

/// <summary>
/// One handler of the startup scenario: its service and implementation type and the decorator pipeline as the
/// generated <c>AddDecoratR()</c> emits it for referenced decorators (innermost decorator first).
/// </summary>
public sealed record StartupEntry(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type ImplementationType,
    Func<ServiceDescriptor, ServiceDescriptor> Pipeline)
{
    public static StartupEntry For<TKey>() => new(
        typeof(IRequestHandler<KeyedRequest<TKey>, int>),
        typeof(KeyedHandler<TKey>),
        static descriptor =>
        {
            descriptor = DecoratRDecoratorRegistry.ApplyDecoratR_Runtime_Benchmarks_InnerDecorator<KeyedRequest<TKey>, int>(descriptor);
            descriptor = DecoratRDecoratorRegistry.ApplyDecoratR_Runtime_Benchmarks_MiddleDecorator<KeyedRequest<TKey>, int>(descriptor);
            descriptor = DecoratRDecoratorRegistry.ApplyDecoratR_Runtime_Benchmarks_OuterDecorator<KeyedRequest<TKey>, int>(descriptor);
            return descriptor;
        });
}

internal static class StartupScenario
{
    /// <summary>
    /// Creates <paramref name="count"/> entries with distinct closed request types (<c>KeyedRequest&lt;Left&lt;Right&lt;…Root&gt;&gt;&gt;</c>)
    /// by generic recursion, so a modular monolith with thousands of handlers does not need thousands of declared types.
    /// </summary>
    public static StartupEntry[] Create(int count)
    {
        var depth = 0;
        while (1 << depth < count) depth++;

        var entries = new List<StartupEntry>(count);
        Add<Root>(depth, entries, count);
        return [.. entries];
    }

    private static void Add<TKey>(int depth, List<StartupEntry> entries, int count)
    {
        if (entries.Count == count) return;

        if (depth == 0)
        {
            entries.Add(StartupEntry.For<TKey>());
            return;
        }

        Add<Left<TKey>>(depth - 1, entries, count);
        Add<Right<TKey>>(depth - 1, entries, count);
    }
}
