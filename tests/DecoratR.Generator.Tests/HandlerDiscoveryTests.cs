using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

public class HandlerDiscoveryTests : GeneratorTestBase
{
    [Fact]
    public void CommandHandler_IsDiscoveredAndRegistered()
    {
        var result = RunGenerator(TestSources.HandlerOnly()).ShouldCompile();

        result.Sources.Keys.Should().Equal(GenerationResult.HandlerRegistryHint);
        result.HandlerRegistry.Should().Contain("typeof(global::TestCommandHandler)");
        result.HandlerRegistry.Should().Contain("global::DecoratR.IRequestHandler<global::TestCommand, string>");
    }

    [Fact]
    public void QueryHandler_IsDiscoveredAndRegistered()
    {
        var result = RunGenerator($"""
            using DecoratR;

            {TestSources.MetadataAttribute}

            {TestSources.TestQueryRecord}

            {TestSources.TestQueryHandler}
            """).ShouldCompile();

        result.HandlerRegistry.Should().Contain("TestQueryHandler");
    }

    [Fact]
    public void MultipleHandlers_AreAllDiscovered()
    {
        var result = RunGenerator("""
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
            """).ShouldCompile();

        result.HandlerRegistry.Should().Contain("Command1Handler");
        result.HandlerRegistry.Should().Contain("Query1Handler");
        result.Diagnostic("DCTR002").GetMessage().Should().Contain("2 handler(s)");
    }

    [Fact]
    public void AbstractHandler_IsSkipped()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestCommand(string Name) : IRequest;

            public abstract class AbstractHandler : IRequestHandler<TestCommand, string>
            {
                public abstract ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default);
            }
            """);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR001");
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public void OpenGenericHandler_IsSkipped()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public class GenericHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => throw new System.NotImplementedException();
            }
            """);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR001");
    }

    [Fact]
    public void InternalHandler_IsIncludedInOutput()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestCommand(string Name) : IRequest;

            internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """).ShouldCompile();

        result.HandlerRegistry.Should().Contain("typeof(global::InternalHandler)");
    }

    [Fact]
    public void RecordHandler_IsDiscovered()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestCommand(string Name) : IRequest;

            public sealed record RecordHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """).ShouldCompile();

        result.HandlerRegistry.Should().Contain("typeof(global::RecordHandler)");
    }

    [Fact]
    public void PartialHandler_IsRegisteredOnce()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record TestCommand(string Name) : IRequest;

            public sealed partial class PartialHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }

            public sealed partial class PartialHandler
            {
                public int Extra => 1;
            }
            """).ShouldCompile();

        result.Registrations.CountOccurrences("typeof(global::PartialHandler)").Should().Be(1);
        result.Diagnostic("DCTR002").GetMessage().Should().Contain("1 handler(s)");
    }

    [Fact]
    public void PartialHandler_WithBaseListOnLaterPart_IsDiscovered()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record TestCommand(string Name) : IRequest;

            public sealed partial class PartialHandler
            {
                public int Extra => 1;
            }

            public sealed partial class PartialHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """).ShouldCompile();

        result.Registrations.CountOccurrences("typeof(global::PartialHandler)").Should().Be(1);
    }

    [Fact]
    public void HandlerImplementingTwoHandlerInterfaces_RegistersBoth()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record CommandA : IRequest;
            public sealed record CommandB : IRequest;

            public sealed class DualHandler : IRequestHandler<CommandA, string>, IRequestHandler<CommandB, int>
            {
                public ValueTask<string> HandleAsync(CommandA request, CancellationToken cancellationToken = default) => default;
                public ValueTask<int> HandleAsync(CommandB request, CancellationToken cancellationToken = default) => default;
            }
            """).ShouldCompile();

        result.Registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::CommandA, string>)");
        result.Registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::CommandB, int>)");
        result.Registrations.CountOccurrences("typeof(global::DualHandler)").Should().Be(2);
    }

    [Fact]
    public void HandlerNestedInGenericType_IsSkipped()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record TestCommand(string Name) : IRequest;

            public static class Outer<T>
            {
                public sealed class NestedHandler : IRequestHandler<TestCommand, string>
                {
                    public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => default;
                }
            }
            """).ShouldCompile();

        result.Registrations.Should().NotContain("NestedHandler");
    }

    [Fact]
    public void NoHandlersFound_EmitsDCTR001WarningAtAttribute()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public class NotAHandler { }
            """);

        var diagnostic = result.Diagnostic("DCTR001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("[GenerateDecoratRMetadata]");
        result.LocationText(diagnostic).Should().Contain("GenerateDecoratRMetadata");
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public void HandlersFound_EmitsHiddenDCTR002Diagnostic()
    {
        var result = RunGenerator(TestSources.HandlerOnly());

        result.Diagnostic("DCTR002").Severity.Should().Be(DiagnosticSeverity.Hidden);
    }

    [Fact]
    public void GeneratedNamespace_MatchesAssemblyName()
    {
        var result = RunGenerator(TestSources.HandlerOnly(), "My.Test.Assembly").ShouldCompile();

        result.HandlerRegistry.Should().Contain("namespace My.Test.Assembly;");
    }
}
