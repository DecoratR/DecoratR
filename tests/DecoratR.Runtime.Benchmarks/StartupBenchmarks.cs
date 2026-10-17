using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using DecoratR.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Runtime.Benchmarks;

/// <summary>
/// Cost of the registration code of <c>AddDecoratR()</c> for a modular monolith with <see cref="Handlers"/> handlers
/// from referenced modules and three decorators each, on top of the registrations of an ASP.NET Core host.
/// </summary>
/// <remarks>
/// The decorator steps call the real generated <c>Apply…</c> methods of <see cref="DecoratRDecoratorRegistry"/>, the
/// handler entries are <see cref="HandlerRegistration"/>s with factories like a generated module registry. The body of
/// <see cref="AddDecoratR"/>, <see cref="AddHandlers"/>, <see cref="Decorate"/> and <see cref="Pipeline"/> mirror the
/// code <c>RegistrationsEmitter</c> and <c>RuntimeHelpersEmitter</c> emit for referenced handlers; keep them in sync
/// when that code changes.
/// </remarks>
[MemoryDiagnoser]
public class StartupBenchmarks
{
    private ServiceDescriptor[] _hostServices = null!;
    private StartupEntry[] _entries = null!;
    private HandlerRegistration[] _registrations = null!;

    [Params(100, 500, 2000)]
    public int Handlers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddProblemDetails();
        _hostServices = [.. builder.Services];
        _entries = StartupScenario.Create(Handlers);
        _registrations = [.. _entries.Select(entry => entry.Registration)];

        // The pipelines must resolve to the outermost decorator; otherwise the benchmark measures a broken setup.
        using var provider = AddDecoratR().AddSingleton<Repository>().AddSingleton<Dependency>().BuildServiceProvider();
        var first = provider.GetRequiredService(_registrations[0].ServiceType);
        if (first.GetType().GetGenericTypeDefinition() != typeof(OuterDecorator<,>))
            throw new InvalidOperationException($"Unexpected pipeline for {_registrations[0].ServiceType}: {first.GetType()}.");
    }

    [Benchmark(Baseline = true, Description = "Host services + handler registrations, no decoration")]
    public IServiceCollection RegisterOnly()
    {
        var services = CreateHostServices();
        AddHandlers(services, _registrations, ServiceLifetime.Transient);
        return services;
    }

    [Benchmark(Description = "AddDecoratR() as generated")]
    public IServiceCollection AddDecoratR()
    {
        var services = CreateHostServices();
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(Marker))
            {
                return services;
            }
        }

        services.Add(new ServiceDescriptor(typeof(Marker), Marker.Instance));
        AddHandlers(services, _registrations, ServiceLifetime.Transient);

        // The generated code adds one entry per decorated service type with a cached static lambda.
        var pipelines = new Dictionary<Type, Pipeline>(_entries.Length);
        foreach (var entry in _entries) pipelines.Add(entry.Registration.ServiceType, new Pipeline(entry.Pipeline));
        Decorate(services, pipelines);

        return services;
    }

    private IServiceCollection CreateHostServices()
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var descriptor in _hostServices) services.Add(descriptor);
        return services;
    }

    /// <summary>Copy of the generated <c>AddHandlers</c> helper.</summary>
    private static void AddHandlers(IServiceCollection services, ReadOnlySpan<HandlerRegistration> handlers, ServiceLifetime lifetime)
    {
        foreach (ref readonly var handler in handlers)
        {
            services.Add(handler.Factory is { } factory
                ? ServiceDescriptor.Describe(handler.ServiceType, factory, lifetime)
                : new ServiceDescriptor(handler.ServiceType, handler.ImplementationType, lifetime));
            services.Add(new ServiceDescriptor(handler.ImplementationType, DecoratRServiceKeys.Validation, handler.ImplementationType, lifetime));
        }
    }

    /// <summary>Copy of the generated <c>Decorate</c> helper.</summary>
    private static void Decorate(IServiceCollection services, Dictionary<Type, Pipeline> pipelines)
    {
        var applied = 0;
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.IsKeyedService)
            {
                continue;
            }

            ref var pipeline = ref CollectionsMarshal.GetValueRefOrNullRef(pipelines, descriptor.ServiceType);
            if (Unsafe.IsNullRef(ref pipeline))
            {
                continue;
            }

            services[i] = pipeline.Apply(descriptor);
            if (!pipeline.Applied)
            {
                pipeline.Applied = true;
                applied++;
            }
        }

        if (applied == pipelines.Count)
        {
            return;
        }

        var missing = new List<Type>();
        foreach (var pair in pipelines)
        {
            if (!pair.Value.Applied)
            {
                missing.Add(pair.Key);
            }
        }

        throw new InvalidOperationException($"DecoratR: no registration of '{string.Join("', '", missing)}' was found to decorate.");
    }

    /// <summary>Copy of the generated <c>Pipeline</c> entry type.</summary>
    private struct Pipeline(Func<ServiceDescriptor, ServiceDescriptor> apply)
    {
        public readonly Func<ServiceDescriptor, ServiceDescriptor> Apply = apply;
        public bool Applied;
    }

    /// <summary>Copy of the generated <c>Marker</c> type.</summary>
    private sealed class Marker
    {
        public static readonly Marker Instance = new();
    }
}
