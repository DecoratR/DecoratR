using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Runtime.Benchmarks;

/// <summary>
/// Cost of the registration code of <c>AddDecoratR()</c> for a modular monolith with <see cref="Handlers"/> handlers
/// from referenced modules and three decorators each, on top of the registrations of an ASP.NET Core host.
/// </summary>
/// <remarks>
/// The decorator steps call the real generated <c>Apply…</c> methods of <see cref="DecoratRDecoratorRegistry"/>.
/// The registration loop and <see cref="Decorate"/> mirror the code <c>RegistrationsEmitter</c> and
/// <c>RuntimeHelpersEmitter.WriteDecorate</c> emit for referenced handlers; keep them in sync when that code changes.
/// </remarks>
[MemoryDiagnoser]
public class StartupBenchmarks
{
    private ServiceDescriptor[] _hostServices = null!;
    private StartupEntry[] _entries = null!;

    [Params(100, 500, 2000)]
    public int Handlers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddProblemDetails();
        _hostServices = [.. builder.Services];
        _entries = StartupScenario.Create(Handlers);

        // The pipelines must resolve to the outermost decorator; otherwise the benchmark measures a broken setup.
        using var provider = AddDecoratR().AddSingleton<Repository>().AddSingleton<Dependency>().BuildServiceProvider();
        var first = provider.GetRequiredService(_entries[0].ServiceType);
        if (first.GetType().GetGenericTypeDefinition() != typeof(OuterDecorator<,>))
            throw new InvalidOperationException($"Unexpected pipeline for {_entries[0].ServiceType}: {first.GetType()}.");
    }

    [Benchmark(Baseline = true, Description = "Host services + handler registrations, no decoration")]
    public IServiceCollection RegisterOnly()
    {
        var services = CreateHostServices();
        foreach (var entry in _entries)
            services.Add(new ServiceDescriptor(entry.ServiceType, entry.ImplementationType, ServiceLifetime.Transient));

        return services;
    }

    [Benchmark(Description = "AddDecoratR() as generated today")]
    public IServiceCollection AddDecoratR()
    {
        var services = CreateHostServices();
        foreach (var entry in _entries)
            services.Add(new ServiceDescriptor(entry.ServiceType, entry.ImplementationType, ServiceLifetime.Transient));

        foreach (var entry in _entries)
            Decorate(services, entry.ServiceType, entry.Pipeline);

        return services;
    }

    private IServiceCollection CreateHostServices()
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var descriptor in _hostServices) services.Add(descriptor);
        return services;
    }

    /// <summary>Copy of the generated <c>Decorate</c> helper.</summary>
    private static void Decorate(IServiceCollection services, Type serviceType, Func<ServiceDescriptor, ServiceDescriptor> decorate)
    {
        var decorated = false;
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.IsKeyedService || descriptor.ServiceType != serviceType)
            {
                continue;
            }

            services[i] = decorate(descriptor);
            decorated = true;
        }

        if (!decorated)
        {
            throw new InvalidOperationException($"DecoratR: no registration of '{serviceType}' was found to decorate.");
        }
    }
}
