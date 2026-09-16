using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class StreamRegistrationTests : GeneratorTestBase
{
    [Fact]
    public void StreamHandler_RegisteredInAddDecoratR()
    {
        var registrations = RunGenerator(TestSources.StreamFullPath()).ShouldCompile().Registrations;

        registrations.Should().Contain("typeof(global::DecoratR.IStreamRequestHandler<global::TestStreamQuery, string>),");
        registrations.Should().Contain("typeof(global::TestStreamQueryHandler),");
    }

    [Fact]
    public void StreamHandlerAndDecorator_GeneratesStreamPipeline()
    {
        var registrations = RunGenerator(TestSources.StreamFullPath(TestSources.StreamDecorator("StreamLoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.GetPipeline("global::TestStreamQuery", "string", isStream: true).Should().Contain(
            "Wrap<global::DecoratR.IStreamRequestHandler<global::TestStreamQuery, string>, global::StreamLoggingDecorator<global::TestStreamQuery, string>>(descriptor);");
    }

    [Fact]
    public void MixedHandlersAndDecorators_AllRegisteredCorrectly()
    {
        var registrations = RunGenerator(TestSources.MixedFullPath(
            TestSources.Decorator("LoggingDecorator", 1) + "\n" +
            TestSources.StreamDecorator("StreamLoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps().Should().ContainSingle().Which.Should().Contain("LoggingDecorator<");
        registrations.GetPipeline("global::TestStreamQuery", "string", isStream: true).GetPipelineSteps().Should().ContainSingle().Which.Should().Contain("StreamLoggingDecorator<");
    }

    [Fact]
    public void RegularDecorator_DoesNotApplyToStreamHandlers()
    {
        var registrations = RunGenerator(TestSources.StreamFullPath(TestSources.Decorator("RegularDecorator", 1))).ShouldCompile().Registrations;

        registrations.TryGetPipeline("global::TestStreamQuery", "string", isStream: true).Should().BeNull();
        registrations.Should().NotContain("RegularDecorator");
    }

    [Fact]
    public void StreamDecorator_DoesNotApplyToRegularHandlers()
    {
        var registrations = RunGenerator(TestSources.FullPath(TestSources.StreamDecorator("StreamOnlyDecorator", 1))).ShouldCompile().Registrations;

        registrations.TryGetPipeline("global::TestCommand", "string").Should().BeNull();
        registrations.Should().NotContain("StreamOnlyDecorator");
    }
}
