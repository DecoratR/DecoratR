using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class OptionsGenerationTests : GeneratorTestBase
{
    [Fact]
    public void OptionsClass_ContainsLifetimePropertyWithTransientDefault()
    {
        var source = TestSources.FullPath();

        var (_, generatedTrees) = RunGenerator(source);

        var optionsSource = generatedTrees.FindSource("class DecoratROptions");
        optionsSource.Should().Contain("ServiceLifetime Lifetime");
        optionsSource.Should().Contain("ServiceLifetime.Transient");
    }

    [Fact]
    public void AddDecoratRMethod_AcceptsOptionalConfigureDelegate()
    {
        var source = TestSources.FullPath();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("Action<global::DecoratR.DecoratROptions>? configure = null");
        registrations.Should().Contain("var options = new global::DecoratR.DecoratROptions();");
        registrations.Should().Contain("configure?.Invoke(options);");
    }

    [Fact]
    public void HandlerRegistrations_UseOptionsLifetimeNotHardcoded()
    {
        var source = TestSources.FullPath();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("options.Lifetime));");
        registrations.Should().NotContain("ServiceLifetime.Transient");
    }
}
