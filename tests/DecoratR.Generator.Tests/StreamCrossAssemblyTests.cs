using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class StreamCrossAssemblyTests : GeneratorTestBase
{
    [Fact]
    public void CompositionRoot_RegistersReferencedStreamHandlersThroughRegistry()
    {
        var (library, host) = RunTwoStageGenerator(TestSources.StreamHandlerOnly(), TestSources.EmptyHost());

        library.HandlerRegistry.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>");
        host.ShouldCompile();
        host.Registrations.Should().Contain("foreach (var handler in global::HandlerLib.DecoratRHandlerRegistry.Handlers)");
    }

    [Fact]
    public void StreamDecoratorInLib_DiscoveredByCompositionRoot()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.StreamHandlerOnly(TestSources.StreamDecorator("AppStreamDecorator", 1)),
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::TestStreamQuery", "string", isStream: true)
            .Should().Contain("global::HandlerLib.DecoratRDecoratorRegistry.ApplyAppStreamDecorator<global::TestStreamQuery, string>(descriptor);");
    }

    [Fact]
    public void MixedLocalAndReferencedStreamHandlers_BothRegistered()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record RemoteStreamQuery(string Filter) : IStreamRequest;

            public sealed class RemoteStreamHandler : IStreamRequestHandler<RemoteStreamQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(RemoteStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    await Task.Yield();
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
                    await Task.Yield();
                    yield return "local";
                }
            }
            """);

        host.ShouldCompile();
        host.Registrations.Should().Contain("global::HandlerLib.DecoratRHandlerRegistry.Handlers");
        host.Registrations.Should().Contain("typeof(global::LocalStreamHandler)");
    }

    [Fact]
    public void CrossAssembly_LocalStreamDecoratorsAppliedToReferencedStreamServiceTypes()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.StreamHandlerOnly(),
            TestSources.EmptyHost(TestSources.StreamDecorator("LocalStreamDecorator", 1)));

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::TestStreamQuery", "string", isStream: true)
            .Should().Contain("Wrap<global::DecoratR.IStreamRequestHandler<global::TestStreamQuery, string>, global::LocalStreamDecorator<global::TestStreamQuery, string>>(descriptor);");
    }
}
