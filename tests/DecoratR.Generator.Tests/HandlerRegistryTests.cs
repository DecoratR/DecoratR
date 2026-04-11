using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class HandlerRegistryTests : GeneratorTestBase
{
    [Fact]
    public void GeneratesRegistryClass_WithHandlersPropertyOnly()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("Handlers");
        registrations.Should().Contain("TestCommandHandler");
        registrations.Should().NotContain("ServiceDescriptor");
        registrations.Should().NotContain("IServiceCollection");
    }

    [Fact]
    public void RegistryClass_DoesNotIncludeRegistrationMethodMarker()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().NotContain("[global::DecoratR.DecoratRRegistrationMethod]");
    }

    [Fact]
    public void EmitsAssemblyLevelRegistrationAttribute()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("[assembly: global::DecoratR.DecoratRHandlerRegistration(");
        registrations.Should().Contain("TestAssembly.DecoratRHandlerRegistry");
    }

    [Fact]
    public void EmitsAssemblyLevelServiceTypeAttributes()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("[assembly: global::DecoratR.DecoratRHandlerServiceType(");
        registrations.Should().Contain("\"global::TestCommand\"");
        registrations.Should().Contain("\"string\"");
    }

    [Fact]
    public void InternalHandler_IncludedInRegistry()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record TestCommand(string Name) : IRequest;

                     internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("InternalHandler");
    }

    [Fact]
    public void RegistryNamespace_MatchesAssemblyName()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source, "My.Test.Assembly");

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("namespace My.Test.Assembly;");
        registrations.Should().Contain("My.Test.Assembly.DecoratRHandlerRegistry");
    }

    [Fact]
    public void DecoratorOnlyAssembly_EmitsDecoratorRegistryWithoutHandlerRegistry()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.MetadataAttribute}

                      {TestSources.Decorator("FooDecorator", 3)}
                      """;

        var (diagnostics, generatedTrees) = RunGenerator(source, "DecoratorLib");

        diagnostics.Should().NotContain(d => d.Id == "DCTR001");
        diagnostics.Should().Contain(d => d.Id == "DCTR003");
        generatedTrees.Should().NotContain(t => t.Contains("DecoratRHandlerRegistry"));

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("ApplyFooDecorator");
        decoratorRegistry.Should().Contain("[assembly: global::DecoratR.DecoratRDecoratorRegistration(");
        decoratorRegistry.Should().Contain(", 3)]");
    }
}