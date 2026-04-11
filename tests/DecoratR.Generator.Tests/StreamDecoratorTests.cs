using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class StreamDecoratorTests : GeneratorTestBase
{
    [Fact]
    public void StreamDecorator_IsRecognizedAsStream()
    {
        var source = TestSources.StreamHandlerOnly(TestSources.StreamDecorator("StreamLoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("ApplyStreamStreamLoggingDecorator");
        decoratorRegistry.Should().Contain("DecorateStreamService<");
        decoratorRegistry.Should().Contain("IStreamRequestHandler");
    }

    [Fact]
    public void StreamDecorator_AssemblyAttribute_UsesStreamAttribute()
    {
        var source = TestSources.StreamHandlerOnly(TestSources.StreamDecorator("StreamLoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("DecoratRStreamDecoratorRegistration");
    }

    [Fact]
    public void RegularDecorator_IsNotRecognizedAsStream()
    {
        var source = TestSources.HandlerOnly(TestSources.Decorator("LoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("ApplyLoggingDecorator");
        decoratorRegistry.Should().NotContain("ApplyStreamLoggingDecorator");
        decoratorRegistry.Should().Contain("DecorateService<");
        decoratorRegistry.Should().NotContain("DecorateStreamService<");
    }

    [Fact]
    public void StreamDecorator_OrderIsRespected()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.TestStreamQueryRecord}
                      {TestSources.TestStreamQueryHandler}

                      {TestSources.StreamDecorator("InnerStreamDecorator", 2)}
                      {TestSources.StreamDecorator("OuterStreamDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetSectionBetween("// Apply stream decorators", "return services;");

        var innerIdx = decorateSection.IndexOf("InnerStreamDecorator", StringComparison.Ordinal);
        var outerIdx = decorateSection.IndexOf("OuterStreamDecorator", StringComparison.Ordinal);
        innerIdx.Should().BeLessThan(outerIdx,
            "because InnerStreamDecorator (Order=2) should be applied first (closer to handler)");
    }

    [Fact]
    public void MixedDecorators_BothTypesEmitted()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.MetadataAttribute}

                      {TestSources.TestCommandRecord}
                      {TestSources.TestCommandHandler}

                      {TestSources.TestStreamQueryRecord}
                      {TestSources.TestStreamQueryHandler}

                      {TestSources.Decorator("RegularDecorator", 1)}
                      {TestSources.StreamDecorator("StreamDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("ApplyRegularDecorator");
        decoratorRegistry.Should().Contain("ApplyStreamStreamDecorator");
        decoratorRegistry.Should().Contain("DecorateService<");
        decoratorRegistry.Should().Contain("DecorateStreamService<");
    }
}
