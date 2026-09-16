using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class StreamHandlerDiscoveryTests : GeneratorTestBase
{
    [Fact]
    public void StreamHandler_IsDiscoveredAndRegistered()
    {
        var registry = RunGenerator(TestSources.StreamHandlerOnly()).ShouldCompile().HandlerRegistry;

        registry.Should().Contain("new(typeof(global::DecoratR.IStreamRequestHandler<global::TestStreamQuery, string>), typeof(global::TestStreamQueryHandler)),");
        registry.Should().Contain("[assembly: global::DecoratR.Metadata.DecoratRHandler(\"global::TestStreamQueryHandler\", \"global::TestStreamQuery\", \"string\", true, ");
    }

    [Fact]
    public void AbstractStreamHandler_IsSkipped()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestStreamQuery(string Filter) : IStreamRequest;

            public abstract class AbstractStreamHandler : IStreamRequestHandler<TestStreamQuery, string>
            {
                public abstract IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, CancellationToken cancellationToken = default);
            }
            """);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR001");
    }

    [Fact]
    public void OpenGenericStreamHandler_IsSkipped()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestStreamQuery(string Filter) : IStreamRequest;

            public class GenericHandler<T> : IStreamRequestHandler<TestStreamQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    await Task.Yield();
                    yield return "item";
                }
            }
            """);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR001");
    }

    [Fact]
    public void MixedHandlers_BothDiscovered()
    {
        var registry = RunGenerator($"""
            using DecoratR;

            {TestSources.MetadataAttribute}

            {TestSources.TestCommandRecord}
            {TestSources.TestStreamQueryRecord}

            {TestSources.TestCommandHandler}

            {TestSources.TestStreamQueryHandler}
            """).ShouldCompile().HandlerRegistry;

        registry.Should().Contain("IRequestHandler<global::TestCommand, string>), typeof(global::TestCommandHandler)");
        registry.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>), typeof(global::TestStreamQueryHandler)");
    }

    [Fact]
    public void DecoratorAnnotatedStreamHandler_IsExcludedFromHandlers()
    {
        var registry = RunGenerator(TestSources.StreamHandlerOnly(TestSources.StreamDecorator("LoggingStreamDecorator", 1))).ShouldCompile().HandlerRegistry;

        registry.Should().Contain("TestStreamQueryHandler");
        registry.Should().NotContain("LoggingStreamDecorator");
    }
}
