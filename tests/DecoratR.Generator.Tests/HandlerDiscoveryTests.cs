using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class HandlerDiscoveryTests : GeneratorTestBase
{
    [Fact]
    public void CommandHandler_IsDiscoveredAndRegistered()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source);

        generatedTrees.Should().HaveCount(6);
        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("TestCommandHandler");
        registrations.Should().Contain("TestCommand");
        registrations.Should().Contain("IRequestHandler");
    }

    [Fact]
    public void QueryHandler_IsDiscoveredAndRegistered()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.MetadataAttribute}

                      {TestSources.TestQueryRecord}

                      {TestSources.TestQueryHandler}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("TestQueryHandler");
    }

    [Fact]
    public void MultipleHandlers_AreAllDiscovered()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record Command1(string Name) : IRequest;
                     public sealed record Query1(string Id) : IRequest;

                     public sealed class Command1Handler : IRequestHandler<Command1, string>
                     {
                         public ValueTask<string> HandleAsync(Command1 request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public sealed class Query1Handler : IRequestHandler<Query1, int>
                     {
                         public ValueTask<int> HandleAsync(Query1 request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult(42);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("Command1Handler");
        registrations.Should().Contain("Query1Handler");
    }

    [Fact]
    public void AbstractHandler_IsSkippedWithWarning()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public sealed record TestCommand(string Name) : IRequest;

                     public abstract class AbstractHandler : IRequestHandler<TestCommand, string>
                     {
                         public abstract ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default);
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR001");
    }

    [Fact]
    public void OpenGenericHandler_IsSkippedWithWarning()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public class GenericHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => throw new System.NotImplementedException();
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR001");
    }

    [Fact]
    public void InternalHandler_IsIncludedInOutput()
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
    public void NoHandlersFound_EmitsDCTR001Warning()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRMetadata]

                     public class NotAHandler { }
                     """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR001");
        generatedTrees.Should().NotContain(t => t.Contains("DecoratRHandlerRegistry"));
    }

    [Fact]
    public void HandlersFound_EmitsDCTR002InfoDiagnostic()
    {
        var source = TestSources.HandlerOnly();

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().Contain(d => d.Id == "DCTR002");
    }

    [Fact]
    public void GeneratedNamespace_MatchesAssemblyName()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source, "My.Test.Assembly");

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().Contain("namespace My.Test.Assembly;");
    }
}
