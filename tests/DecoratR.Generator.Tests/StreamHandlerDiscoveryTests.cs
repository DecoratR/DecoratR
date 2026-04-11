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
        var source = TestSources.StreamHandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("TestStreamQueryHandler");
        registrations.Should().Contain("TestStreamQuery");
        registrations.Should().Contain("IStreamRequestHandler");
        registrations.Should().Contain("StreamHandlers");
    }

    [Fact]
    public void AbstractStreamHandler_IsSkipped()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record TestStreamQuery(string Filter) : IStreamRequest;

                     public abstract class AbstractStreamHandler : IStreamRequestHandler<TestStreamQuery, string>
                     {
                         public abstract IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, CancellationToken cancellationToken = default);
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR001");
    }

    [Fact]
    public void OpenGenericStreamHandler_IsSkipped()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record TestStreamQuery(string Filter) : IStreamRequest;

                     public class GenericHandler<T> : IStreamRequestHandler<TestStreamQuery, string>
                     {
                         public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, CancellationToken cancellationToken = default)
                         {
                             yield return "item";
                         }
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR001");
    }

    [Fact]
    public void MixedHandlers_BothDiscovered()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record TestCommand(string Name) : IRequest;
                     public sealed record TestStreamQuery(string Filter) : IStreamRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public sealed class TestStreamQueryHandler : IStreamRequestHandler<TestStreamQuery, string>
                     {
                         public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                         {
                             yield return "item";
                         }
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("TestCommandHandler");
        registrations.Should().Contain("IRequestHandler<global::TestCommand, string>");
        registrations.Should().Contain("TestStreamQueryHandler");
        registrations.Should().Contain("IStreamRequestHandler<global::TestStreamQuery, string>");
    }

    [Fact]
    public void DecoratorAnnotatedStreamHandler_IsExcludedFromStreamHandlers()
    {
        var source = $$"""
                      using DecoratR;

                      [assembly: DecoratR.GenerateDecoratRMetadata]

                      public sealed record TestStreamQuery(string Filter) : IStreamRequest;

                      public sealed class TestStreamQueryHandler : IStreamRequestHandler<TestStreamQuery, string>
                      {
                          public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                          {
                              yield return "item";
                          }
                      }

                      {{TestSources.StreamDecorator("LoggingStreamDecorator", 1)}}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("TestStreamQueryHandler");
        registrations.Should().NotContain("LoggingStreamDecorator");
    }
}
