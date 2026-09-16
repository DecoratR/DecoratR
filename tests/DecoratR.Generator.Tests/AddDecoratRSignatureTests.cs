using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class AddDecoratRSignatureTests : GeneratorTestBase
{
    [Fact]
    public void AddDecoratRMethod_AcceptsOptionalConfigureDelegate()
    {
        var registrations = RunGenerator(TestSources.FullPath()).ShouldCompile().Registrations;

        registrations.Should().Contain("this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
        registrations.Should().Contain("global::System.Action<global::DecoratR.DecoratROptions>? configure = null)");
        registrations.Should().Contain("var options = new global::DecoratR.DecoratROptions();");
        registrations.Should().Contain("configure?.Invoke(options);");
    }

    [Fact]
    public void OptionsClass_IsNotGenerated_ItLivesInAbstractions()
    {
        var result = RunGenerator(TestSources.FullPath());

        result.Sources.Values.Should().NotContain(s => s.Contains("class DecoratROptions"));
        typeof(DecoratROptions).Assembly.GetName().Name.Should().Be("DecoratR.Abstractions");
    }

    [Fact]
    public void HandlerRegistrations_UseOptionsLifetimeNotHardcoded()
    {
        var registrations = RunGenerator(TestSources.FullPath()).ShouldCompile().Registrations;

        registrations.Should().Contain("options.Lifetime));");
        registrations.Should().NotContain("ServiceLifetime.Transient");
    }
}
