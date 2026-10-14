using BenchmarkDotNet.Running;
using DecoratR.Runtime.Benchmarks;

// dotnet run -c Release -- --filter '*Resolve*'
// dotnet run -c Release -- --filter '*Startup*' --job short
BenchmarkSwitcher.FromAssembly(typeof(ResolveBenchmarks).Assembly).Run(args);
