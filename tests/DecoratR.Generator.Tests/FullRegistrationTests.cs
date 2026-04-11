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
        var source = TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("TestCommandHandler");
        registrations.Should().Contain("LoggingDecorator");
        registrations.Should().Contain("DecorateService<");
    }

    [Fact]
    public void Decorator_IsExcludedFromHandlerRegistrations()
    {
        var source = TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("DecorateService<");
        registrations.Should().Contain("LoggingDecorator");
        registrations.Should().Contain("TestCommandHandler");

        var applySection = registrations.GetDecoratorApplicationSection();
        applySection.Should().NotContain("ServiceDescriptor");
    }

    [Fact]
    public void MultipleDecorators_AreAllApplied()
    {
        var source = TestSources.FullPath(
            TestSources.Decorator("ADecorator", 1) + "\n" +
            TestSources.Decorator("BDecorator", 2));

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ADecorator");
        registrations.Should().Contain("BDecorator");
    }

    [Fact]
    public void DecoratorsDiscovered_EmitsDCTR003InfoDiagnostic()
    {
        var source = TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1));

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR003");
    }

    [Fact]
    public void DecoratorOrder_HigherOrderAppliedCloserToHandler()
    {
        var source = TestSources.FullPath(
            TestSources.Decorator("InnerDecorator", 2) + "\n" +
            TestSources.Decorator("OuterDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        var innerDecorateIdx = decorateSection.IndexOf("InnerDecorator", StringComparison.Ordinal);
        var outerDecorateIdx = decorateSection.IndexOf("OuterDecorator", StringComparison.Ordinal);
        innerDecorateIdx.Should().BeLessThan(outerDecorateIdx,
            "because InnerDecorator (Order=2) should be applied first (closer to handler)");
    }

    [Fact]
    public void LocalHandlers_EmittedWithLocalCommentMarker()
    {
        var source = TestSources.FullPath();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("// Register local handlers");
        registrations.Should().Contain("TestCommandHandler");
    }
}