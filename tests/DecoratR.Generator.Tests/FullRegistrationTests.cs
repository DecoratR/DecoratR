using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class FullRegistrationTests : GeneratorTestBase
{
    [Fact]
    public void HandlerAndDecorator_GeneratesAddDecoratRExtension()
    {
        var result = RunGenerator(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile();

        result.Sources.Keys.Should().Equal(GenerationResult.RegistrationsHint);
        result.Registrations.Should().Contain("namespace Microsoft.Extensions.DependencyInjection;");
        result.Registrations.Should().Contain("public static class DecoratRServiceCollectionExtensions");
        result.Registrations.Should().Contain("typeof(global::TestCommandHandler)");
        result.Registrations.GetPipeline("global::TestCommand", "string").Should().Contain(
            "descriptor = Wrap<global::DecoratR.IRequestHandler<global::TestCommand, string>, global::LoggingDecorator<global::TestCommand, string>>(descriptor); // Order 1");
    }

    [Fact]
    public void Decorator_IsExcludedFromHandlerRegistrations()
    {
        var registrations = RunGenerator(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.Should().NotContain("typeof(global::LoggingDecorator");
        registrations.CountOccurrences("services.Add(").Should().Be(1);
    }

    [Fact]
    public void MultipleDecorators_AreAllApplied()
    {
        var registrations = RunGenerator(TestSources.FullPath(
            TestSources.Decorator("ADecorator", 1) + "\n" +
            TestSources.Decorator("BDecorator", 2))).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps();
        steps.Should().HaveCount(2);
        steps.Should().Contain(s => s.Contains("ADecorator"));
        steps.Should().Contain(s => s.Contains("BDecorator"));
    }

    [Fact]
    public void DecoratorsDiscovered_EmitsDCTR003Diagnostic()
    {
        var result = RunGenerator(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1)));

        result.Diagnostic("DCTR003").GetMessage().Should().Contain("1 decorator(s)");
    }

    [Fact]
    public void DecoratorOrder_HigherOrderAppliedCloserToHandler()
    {
        var registrations = RunGenerator(TestSources.FullPath(
            TestSources.Decorator("InnerDecorator", 2) + "\n" +
            TestSources.Decorator("OuterDecorator", 1))).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps();
        steps[0].Should().Contain("InnerDecorator").And.EndWith("// Order 2");
        steps[1].Should().Contain("OuterDecorator").And.EndWith("// Order 1");
    }

    [Fact]
    public void SameOrder_SortedAlphabeticallyByTypeName_OutermostFirstAlphabetically()
    {
        var registrations = RunGenerator(TestSources.FullPath(
            TestSources.Decorator("ZebraDecorator", 1) + "\n" +
            TestSources.Decorator("AlphaDecorator", 1))).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps();
        steps[0].Should().Contain("ZebraDecorator", "the alphabetically last decorator is innermost and applied first");
        steps[1].Should().Contain("AlphaDecorator", "the alphabetically first decorator is outermost and applied last");
    }

    [Fact]
    public void LocalHandlers_EmittedWithLocalCommentMarker()
    {
        var registrations = RunGenerator(TestSources.FullPath()).ShouldCompile().Registrations;

        registrations.Should().Contain("// Handlers declared in this assembly");
        registrations.Should().Contain("typeof(global::TestCommandHandler)");
        registrations.Should().NotContain("Decorate(");
    }

    [Fact]
    public void HostWithoutAnything_StillEmitsEmptyAddDecoratR()
    {
        var result = RunGenerator(TestSources.EmptyHost()).ShouldCompile();

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR001").Which.GetMessage().Should().Contain("[GenerateDecoratRRegistrations]");
        result.Registrations.Should().Contain("AddDecoratR(");
    }

    [Fact]
    public void RuntimeHelpers_UseCreateFactoryAndSkipKeyedServices()
    {
        var registrations = RunGenerator(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.Should().Contain("ActivatorUtilities.CreateFactory(");
        registrations.Should().NotContain("ActivatorUtilities.CreateInstance(");
        registrations.Should().Contain("descriptor.IsKeyedService");
        registrations.Should().Contain("services[i] = decorate(descriptor);");
    }
}
