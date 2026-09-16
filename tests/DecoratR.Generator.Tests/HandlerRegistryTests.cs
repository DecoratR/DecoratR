using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class HandlerRegistryTests : GeneratorTestBase
{
    [Fact]
    public void GeneratesRegistryClass_WithHandlersArray()
    {
        var registry = RunGenerator(TestSources.HandlerOnly()).ShouldCompile().HandlerRegistry;

        registry.Should().Contain("public static class DecoratRHandlerRegistry");
        registry.Should().Contain("public static HandlerRegistration[] Handlers { get; } =");
        registry.Should().Contain("new(typeof(global::DecoratR.IRequestHandler<global::TestCommand, string>), typeof(global::TestCommandHandler)),");
        registry.Should().NotContain("IServiceCollection");
    }

    [Fact]
    public void RegistryClass_IsHiddenFromIntelliSense()
    {
        var registry = RunGenerator(TestSources.HandlerOnly()).HandlerRegistry;

        registry.Should().Contain("EditorBrowsableState.Never");
        registry.Should().Contain("GeneratedCode(\"DecoratR.Generator\"");
    }

    [Fact]
    public void EmitsAssemblyLevelRegistryAttribute()
    {
        var registry = RunGenerator(TestSources.HandlerOnly()).HandlerRegistry;

        registry.Should().Contain("[assembly: global::DecoratR.Metadata.DecoratRRegistry(\"global::TestAssembly.DecoratRHandlerRegistry\")]");
    }

    [Fact]
    public void EmitsAssemblyLevelHandlerAttributes_WithHierarchies()
    {
        var registry = RunGenerator(TestSources.HandlerOnly()).HandlerRegistry;

        registry.Should().Contain("[assembly: global::DecoratR.Metadata.DecoratRHandler(\"global::TestCommandHandler\", \"global::TestCommand\", \"string\", false, ");
        registry.Should().Contain("RequestTypeHierarchy = \"global::TestCommand;");
        registry.Should().Contain("global::DecoratR.IRequest;");
        registry.Should().Contain("!class");
        registry.Should().Contain("ResponseTypeHierarchy = \"string;");
    }

    [Fact]
    public void InternalRequestType_MarksHandlerAsNotPubliclyAccessible()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            internal sealed record TestCommand(string Name) : IRequest;

            internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """).ShouldCompile();

        result.HandlerRegistry.Should().Contain("IsPubliclyAccessible = false");
        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR013");
    }

    [Fact]
    public void RegistryNamespace_IsSanitizedAssemblyName()
    {
        var registry = RunGenerator(TestSources.HandlerOnly(), "My-App.Web").ShouldCompile().HandlerRegistry;

        registry.Should().Contain("namespace My_App.Web;");
        registry.Should().Contain("\"global::My_App.Web.DecoratRHandlerRegistry\"");
    }

    [Fact]
    public void RegistryNamespace_EscapesKeywordSegments()
    {
        var registry = RunGenerator(TestSources.HandlerOnly(), "class.Lib").ShouldCompile().HandlerRegistry;

        registry.Should().Contain("namespace @class.Lib;");
    }

    [Fact]
    public void DecoratorOnlyAssembly_EmitsDecoratorRegistryWithoutHandlerRegistry()
    {
        var result = RunGenerator($"""
            using DecoratR;

            {TestSources.MetadataAttribute}

            {TestSources.Decorator("FooDecorator", 3)}
            """, "DecoratorLib").ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Id == "DCTR001");
        result.Diagnostics.Should().Contain(d => d.Id == "DCTR003");
        result.Has(GenerationResult.HandlerRegistryHint).Should().BeFalse();

        result.DecoratorRegistry.Should().Contain("ApplyFooDecorator<TRequest, TResponse>(");
        result.DecoratorRegistry.Should().Contain("[assembly: global::DecoratR.Metadata.DecoratRDecorator(\"global::DecoratorLib.DecoratRDecoratorRegistry.ApplyFooDecorator\", \"global::FooDecorator\", 3, false");
    }
}
