using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class StreamCrossAssemblyTests : GeneratorTestBase
{
    [Fact]
    public void CompositionRoot_IteratesReferencedStreamHandlerRegistry()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.StreamHandlerOnly(),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("HandlerLib.DecoratRHandlerRegistry.StreamHandlers");
        registrations.Should().Contain("// Register stream handlers from referenced assemblies");
    }

    [Fact]
    public void StreamDecoratorInLib_DiscoveredByCompositionRoot()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.StreamHandlerOnly(TestSources.StreamDecorator("AppStreamDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("AppStreamDecorator");
        registrations.Should().Contain("// Apply stream decorators");
    }

    [Fact]
    public void MixedLocalAndReferencedStreamHandlers_BothRegistered()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record RemoteStreamQuery(string Filter) : IStreamRequest;

            public sealed class RemoteStreamHandler : IStreamRequestHandler<RemoteStreamQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(RemoteStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    yield return "remote";
                }
            }
            """,
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record LocalStreamQuery(string Filter) : IStreamRequest;

            public sealed class LocalStreamHandler : IStreamRequestHandler<LocalStreamQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(LocalStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    yield return "local";
                }
            }
            """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("HandlerLib.DecoratRHandlerRegistry.StreamHandlers");
        registrations.Should().Contain("LocalStreamHandler");
        registrations.Should().Contain("// Register local stream handlers");
        registrations.Should().Contain("// Register stream handlers from referenced assemblies");
    }

    [Fact]
    public void CrossAssembly_StreamDecoratorsAppliedToReferencedStreamServiceTypes()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.StreamHandlerOnly(),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}

             {TestSources.StreamDecorator("LocalStreamDecorator", 1)}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("DecorateStreamService<");
        registrations.Should().Contain("LocalStreamDecorator");
        registrations.Should().Contain("TestStreamQuery");
    }
}
