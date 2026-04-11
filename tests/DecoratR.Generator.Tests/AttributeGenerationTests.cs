using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class AttributeGenerationTests : GeneratorTestBase
{
    [Fact]
    public void WithoutAssemblyAttribute_EmitsSevenAttributeSourcesOnly()
    {
        var source = """
                     using DecoratR;

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        generatedTrees.Should().HaveCount(7);
        generatedTrees.Should().Contain(t => t.Contains("GenerateDecoratRMetadataAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("GenerateDecoratRRegistrationsAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("DecoratRHandlerRegistrationAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("DecoratRHandlerServiceTypeAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("DecoratRDecoratorRegistrationAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("DecoratRStreamHandlerServiceTypeAttribute"));
        generatedTrees.Should().Contain(t => t.Contains("DecoratRStreamDecoratorRegistrationAttribute"));
        generatedTrees.Should().NotContain(t => t.Contains("DecoratRHandlerRegistry"));
    }

    [Fact]
    public void GeneratedAttributes_ContainXmlDocumentation()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var handlerAttribute = generatedTrees.FindSource("GenerateDecoratRMetadataAttribute");
        handlerAttribute.Should().Contain("Generates handler and decorator metadata for the current assembly.");
        handlerAttribute.Should().Contain("class library that defines request handlers or decorators");
        handlerAttribute.Should().Contain("GenerateDecoratRRegistrationsAttribute");

        var fullAttribute = generatedTrees.FindSource("class GenerateDecoratRRegistrationsAttribute");
        fullAttribute.Should()
            .Contain("Generates the <c>AddDecoratR()</c> registration entry point for the current composition root.");
        fullAttribute.Should().Contain("Apply this to the host or startup assembly.");
        fullAttribute.Should().Contain("registers handlers and applies decorators in pipeline order.");
    }
}