using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator.Benchmarks;

/// <summary>
/// Generator cost in a modular monolith: a shared kernel with three cross-cutting decorators, <see cref="Modules"/>
/// module assemblies with <see cref="HandlersPerModule"/> handlers and one module-scoped decorator each, and a host
/// with <c>[GenerateDecoratRRegistrations]</c> that composes all of them.
/// </summary>
/// <remarks>
/// The keystroke benchmarks alternate between two compilations that differ in one syntax tree and reuse the driver
/// state of the previous run, which is what the IDE does on every edit.
/// </remarks>
[MemoryDiagnoser(false)]
public class ModularMonolithBenchmarks
{
    private CSharpCompilation _host = null!;
    private CSharpCompilation _hostEdited = null!;
    private GeneratorDriver _hostDriver = null!;
    private bool _hostToggle;

    private CSharpCompilation _module = null!;
    private CSharpCompilation _moduleEdited = null!;
    private GeneratorDriver _moduleDriver = null!;
    private bool _moduleToggle;

    [Params(10)]
    public int Modules { get; set; }

    [Params(100)]
    public int HandlersPerModule { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sharedKernel = CompilationHelper.EnsureGenerates(
            CompilationHelper.CreateCompilation(ModularMonolithSources.SharedKernel, ModularMonolithSources.SharedKernelAssemblyName),
            CompilationHelper.DecoratorRegistryHint).ToMetadataReference();

        var moduleReferences = new List<MetadataReference>();
        for (var m = 0; m < Modules; m++)
        {
            var module = CreateModule(m, sharedKernel);
            if (m == 0) _module = module;

            moduleReferences.Add(CompilationHelper.EnsureGenerates(
                module, CompilationHelper.HandlerRegistryHint, CompilationHelper.DecoratorRegistryHint).ToMetadataReference());
        }

        _host = CompilationHelper.CreateCompilation(
            [(ModularMonolithSources.HostPath, ModularMonolithSources.Host)],
            ModularMonolithSources.HostAssemblyName,
            [sharedKernel, .. moduleReferences]);
        CompilationHelper.EnsureGenerates(_host, CompilationHelper.RegistrationsHint);

        _hostEdited = CompilationHelper.WithEdit(_host, ModularMonolithSources.HostPath);
        _hostDriver = CompilationHelper.CreateDriver().RunGenerators(_host);

        _moduleEdited = CompilationHelper.WithEdit(_module, ModularMonolithSources.HandlerPath(0, HandlersPerModule / 2));
        _moduleDriver = CompilationHelper.CreateDriver().RunGenerators(_module);
    }

    [Benchmark(Description = "Host: cold run")]
    public GeneratorDriverRunResult HostCold() =>
        CompilationHelper.CreateDriver().RunGenerators(_host).GetRunResult();

    [Benchmark(Description = "Host: keystroke (unrelated edit), warm driver")]
    public GeneratorDriver HostKeystroke()
    {
        _hostToggle = !_hostToggle;
        return _hostDriver = _hostDriver.RunGenerators(_hostToggle ? _hostEdited : _host);
    }

    [Benchmark(Description = "Module: cold run")]
    public GeneratorDriverRunResult ModuleCold() =>
        CompilationHelper.CreateDriver().RunGenerators(_module).GetRunResult();

    [Benchmark(Description = "Module: keystroke in one handler file, warm driver")]
    public GeneratorDriver ModuleKeystroke()
    {
        _moduleToggle = !_moduleToggle;
        return _moduleDriver = _moduleDriver.RunGenerators(_moduleToggle ? _moduleEdited : _module);
    }

    private CSharpCompilation CreateModule(int module, MetadataReference sharedKernel) =>
        CompilationHelper.CreateCompilation(
            ModularMonolithSources.Module(module, HandlersPerModule),
            ModularMonolithSources.ModuleAssemblyName(module),
            [sharedKernel]);
}
