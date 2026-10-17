using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Runtime.Benchmarks;

/// <summary>
/// Cost of resolving one transient pipeline (handler + three decorators, one singleton dependency each) from a scope:
/// the hot path of every request with the default <see cref="ServiceLifetime.Transient"/> lifetime.
/// </summary>
[MemoryDiagnoser]
public class ResolveBenchmarks
{
    private readonly List<ServiceProvider> _providers = [];
    private IServiceProvider _handlerOnly = null!;
    private IServiceProvider _decoratR = null!;
    private IServiceProvider _handWritten = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handlerOnly = Build(services => services.AddTransient<IRequestHandler<PingCommand, int>, PingHandler>());
        _decoratR = Build(services => services.AddDecoratR());
        _handWritten = Build(services => services.AddTransient<IRequestHandler<PingCommand, int>>(sp =>
            new OuterDecorator<PingCommand, int>(
                new MiddleDecorator<PingCommand, int>(
                    new InnerDecorator<PingCommand, int>(
                        new PingHandler(sp.GetRequiredService<Repository>()),
                        sp.GetRequiredService<Dependency>()),
                    sp.GetRequiredService<Dependency>()),
                sp.GetRequiredService<Dependency>())));

        // The generated pipeline must actually be decorated; otherwise the benchmark compares the wrong thing.
        if (_decoratR.GetRequiredService<IRequestHandler<PingCommand, int>>() is not OuterDecorator<PingCommand, int>)
            throw new InvalidOperationException("AddDecoratR() did not apply the decorators to PingHandler.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var provider in _providers) provider.Dispose();
    }

    [Benchmark(Baseline = true, Description = "MS DI, handler only (no decorators)")]
    public object HandlerOnly() => _handlerOnly.GetRequiredService<IRequestHandler<PingCommand, int>>();

    [Benchmark(Description = "AddDecoratR(), 3 decorators")]
    public object DecoratR() => _decoratR.GetRequiredService<IRequestHandler<PingCommand, int>>();

    [Benchmark(Description = "Lower bound: one hand-written factory, 3 decorators")]
    public object HandWritten() => _handWritten.GetRequiredService<IRequestHandler<PingCommand, int>>();

    private IServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Repository>();
        services.AddSingleton<Dependency>();
        register(services);

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.CreateScope().ServiceProvider;
    }
}
