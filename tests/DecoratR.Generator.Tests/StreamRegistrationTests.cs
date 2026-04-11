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
        var source = TestSources.StreamFullPath();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("// Register local stream handlers");
        registrations.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>");
        registrations.Should().Contain("TestStreamQueryHandler");
    }

    [Fact]
    public void StreamHandlerAndDecorator_GeneratesAddDecoratRWithStreamDecoration()
    {
        var source = TestSources.StreamFullPath(TestSources.StreamDecorator("StreamLoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("// Register local stream handlers");
        registrations.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>");
        registrations.Should().Contain("DecorateStreamService<");
        registrations.Should().Contain("StreamLoggingDecorator");
    }

    [Fact]
    public void MixedHandlersAndDecorators_AllRegisteredCorrectly()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.TestCommandRecord}
                      {TestSources.TestCommandHandler}

                      {TestSources.TestStreamQueryRecord}
                      {TestSources.TestStreamQueryHandler}

                      {TestSources.Decorator("LoggingDecorator", 1)}
                      {TestSources.StreamDecorator("StreamLoggingDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");

        // Regular pipeline
        registrations.Should().Contain("// Register local handlers");
        registrations.Should().Contain("IRequestHandler<global::TestCommand, string>");
        registrations.Should().Contain("// Apply decorators");
        registrations.Should().Contain("DecorateService<");

        // Stream pipeline
        registrations.Should().Contain("// Register local stream handlers");
        registrations.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>");
        registrations.Should().Contain("// Apply stream decorators");
        registrations.Should().Contain("DecorateStreamService<");
    }

    [Fact]
    public void RegularDecorator_DoesNotApplyToStreamHandlers()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.TestStreamQueryRecord}
                      {TestSources.TestStreamQueryHandler}

                      {TestSources.Decorator("RegularDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("// Register local stream handlers");
        // Regular decorator should NOT appear in any DecorateStreamService call
        registrations.Should().NotContain("DecorateStreamService<");
        registrations.Should().NotContain("RegularDecorator");
    }

    [Fact]
    public void StreamDecorator_DoesNotApplyToRegularHandlers()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.TestCommandRecord}
                      {TestSources.TestCommandHandler}

                      {TestSources.StreamDecorator("StreamOnlyDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("// Register local handlers");
        // Stream decorator should NOT appear in any DecorateService call
        registrations.Should().NotContain("DecorateService<");
        registrations.Should().NotContain("StreamOnlyDecorator");
    }
}
