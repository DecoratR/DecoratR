using System.Reflection;
using DecoratR;

[assembly: GenerateHandlerRegistrations]

namespace DecoratR.Sample.Application;

public static class ApplicationAssembly
{
    public static readonly Assembly Assembly = typeof(ApplicationAssembly).Assembly;
}