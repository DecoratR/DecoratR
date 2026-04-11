using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator.Benchmarks;

[MemoryDiagnoser(false)]
public class GeneratorBenchmarks
{
    private CSharpCompilation _singleHandlerCompilation = null!;
    private GeneratorDriver _singleHandlerDriver = null!;

    private CSharpCompilation _multiHandlerCompilation = null!;
    private GeneratorDriver _multiHandlerDriver = null!;

    private CSharpCompilation _constrainedCompilation = null!;
    private GeneratorDriver _constrainedDriver = null!;

    private CSharpCompilation _crossAssemblyCompilation = null!;
    private GeneratorDriver _crossAssemblyDriver = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Scenario 1: Single handler + single decorator (metadata path)
        _singleHandlerCompilation = CompilationHelper.CreateCompilation(Sources.SingleHandlerWithDecorator);
        _singleHandlerDriver = CompilationHelper.CreateDriver();

        // Scenario 2: Many handlers + multiple decorators (metadata path)
        _multiHandlerCompilation = CompilationHelper.CreateCompilation(Sources.MultipleHandlersWithDecorators);
        _multiHandlerDriver = CompilationHelper.CreateDriver();

        // Scenario 3: Constrained decorators with command/query separation (full path)
        _constrainedCompilation = CompilationHelper.CreateCompilation(Sources.ConstrainedDecorators);
        _constrainedDriver = CompilationHelper.CreateDriver();

        // Scenario 4: Cross-assembly composition
        (_crossAssemblyCompilation, _crossAssemblyDriver) = CompilationHelper.CreateTwoStageSetup(
            Sources.CrossAssemblyHandlerLib,
            Sources.CrossAssemblyCompositionRoot);
    }

    [Benchmark(Description = "1 handler + 1 decorator (metadata)")]
    public GeneratorDriverRunResult SingleHandler()
    {
        var driver = _singleHandlerDriver.RunGenerators(_singleHandlerCompilation);
        return driver.GetRunResult();
    }

    [Benchmark(Description = "5 handlers + 3 decorators (metadata)")]
    public GeneratorDriverRunResult MultipleHandlers()
    {
        var driver = _multiHandlerDriver.RunGenerators(_multiHandlerCompilation);
        return driver.GetRunResult();
    }

    [Benchmark(Description = "Constrained decorators (full path)")]
    public GeneratorDriverRunResult ConstrainedDecorators()
    {
        var driver = _constrainedDriver.RunGenerators(_constrainedCompilation);
        return driver.GetRunResult();
    }

    [Benchmark(Description = "Cross-assembly composition")]
    public GeneratorDriverRunResult CrossAssembly()
    {
        var driver = _crossAssemblyDriver.RunGenerators(_crossAssemblyCompilation);
        return driver.GetRunResult();
    }
}
