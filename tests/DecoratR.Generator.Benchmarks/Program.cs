using BenchmarkDotNet.Running;
using DecoratR.Generator.Benchmarks;

// dotnet run -c Release -- --filter '*'            all benchmarks
// dotnet run -c Release -- --filter '*ModularMonolith*' --job short
BenchmarkSwitcher.FromAssembly(typeof(GeneratorBenchmarks).Assembly).Run(args);
