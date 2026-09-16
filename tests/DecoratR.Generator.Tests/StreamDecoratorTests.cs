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
        var registry = RunGenerator(TestSources.StreamHandlerOnly(TestSources.StreamDecorator("StreamLoggingDecorator", 1))).ShouldCompile().DecoratorRegistry;

        registry.Should().Contain("ApplyStreamLoggingDecorator<TRequest, TResponse>(");
        registry.Should().Contain("return Wrap<global::DecoratR.IStreamRequestHandler<TRequest, TResponse>, global::StreamLoggingDecorator<TRequest, TResponse>>(inner);");
        registry.Should().Contain("where TRequest : global::DecoratR.IStreamRequest");
    }

    [Fact]
    public void StreamDecorator_AssemblyAttribute_HasIsStreamTrue()
    {
        var registry = RunGenerator(TestSources.StreamHandlerOnly(TestSources.StreamDecorator("StreamLoggingDecorator", 1))).DecoratorRegistry;

        registry.Should().Contain("\"global::StreamLoggingDecorator\", 1, true, RequestConstraints = \"global::DecoratR.IStreamRequest\")]");
    }

    [Fact]
    public void RegularDecorator_IsNotRecognizedAsStream()
    {
        var registry = RunGenerator(TestSources.HandlerOnly(TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().DecoratorRegistry;

        registry.Should().Contain("\"global::LoggingDecorator\", 1, false, ");
        registry.Should().Contain("Wrap<global::DecoratR.IRequestHandler<TRequest, TResponse>,");
        registry.Should().NotContain("IStreamRequestHandler");
    }

    [Fact]
    public void StreamDecorator_OrderIsRespected()
    {
        var registrations = RunGenerator(TestSources.StreamFullPath(
            TestSources.StreamDecorator("InnerStreamDecorator", 2) + "\n" +
            TestSources.StreamDecorator("OuterStreamDecorator", 1))).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::TestStreamQuery", "string", isStream: true).GetPipelineSteps();
        steps[0].Should().Contain("InnerStreamDecorator");
        steps[1].Should().Contain("OuterStreamDecorator");
    }

    [Fact]
    public void MixedDecorators_BothTypesEmitted()
    {
        var registry = RunGenerator($"""
            using DecoratR;

            {TestSources.MetadataAttribute}

            {TestSources.TestCommandRecord}
            {TestSources.TestCommandHandler}

            {TestSources.TestStreamQueryRecord}
            {TestSources.TestStreamQueryHandler}

            {TestSources.Decorator("RegularDecorator", 1)}
            {TestSources.StreamDecorator("StreamDecorator", 1)}
            """).ShouldCompile().DecoratorRegistry;

        registry.Should().Contain("ApplyRegularDecorator<TRequest, TResponse>(");
        registry.Should().Contain("ApplyStreamDecorator<TRequest, TResponse>(");
        registry.Should().Contain("Wrap<global::DecoratR.IRequestHandler<TRequest, TResponse>, global::RegularDecorator<TRequest, TResponse>>");
        registry.Should().Contain("Wrap<global::DecoratR.IStreamRequestHandler<TRequest, TResponse>, global::StreamDecorator<TRequest, TResponse>>");
    }
}
