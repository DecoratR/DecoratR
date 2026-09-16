using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// What a library exports for composition roots, and what it must keep to itself.
/// </summary>
public class MetadataExportTests : GeneratorTestBase
{
    [Fact]
    public void DCTR009_LibraryWithDecoratorsButWithoutDependencyInjection_IsErrorButHandlerRegistryIsStillEmitted()
    {
        var result = RunGeneratorWithoutDependencyInjection(TestSources.HandlerOnly(TestSources.Decorator("LoggingDecorator", 1)));

        result.Diagnostic("DCTR009").GetMessage().Should().Contain("[GenerateDecoratRMetadata]");
        result.Has(GenerationResult.HandlerRegistryHint).Should().BeTrue();
        result.Has(GenerationResult.DecoratorRegistryHint).Should().BeFalse();
    }

    [Fact]
    public void DCTR014_DecoratorWithNonPublicConstraintType_IsNotExported()
    {
        var (library, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            internal interface IInternalMarker;

            public sealed record MarkedCommand : IRequest, IInternalMarker;

            public sealed class MarkedHandler : IRequestHandler<MarkedCommand, string>
            {
                public ValueTask<string> HandleAsync(MarkedCommand request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            internal sealed class MarkerDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, IInternalMarker
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }

            [Decorator(Order = 2)]
            internal sealed class PublicDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """,
            TestSources.EmptyHost());

        library.Diagnostic("DCTR014").GetMessage().Should().Contain("MarkerDecorator").And.Contain("IInternalMarker");
        library.DecoratorRegistry.Should().NotContain("MarkerDecorator");
        library.DecoratorRegistry.Should().Contain("ApplyPublicDecorator");

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::MarkedCommand", "string").GetPipelineSteps()
            .Should().ContainSingle().Which.Should().Contain("ApplyPublicDecorator");
    }

    [Fact]
    public void DCTR014_OnlyDecoratorWithNonPublicConstraint_EmitsNoDecoratorRegistry()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            internal interface IInternalMarker;

            [Decorator(Order = 1)]
            internal sealed class MarkerDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, IInternalMarker
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """, "HandlerLib").ShouldCompile();

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR014");
        result.Has(GenerationResult.DecoratorRegistryHint).Should().BeFalse();
    }

    [Fact]
    public void NonPublicConstraintType_IsFineForLocalDecoratorsInCompositionRoot()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            internal interface IInternalMarker;

            public sealed record MarkedCommand : IRequest, IInternalMarker;

            public sealed class MarkedHandler : IRequestHandler<MarkedCommand, string>
            {
                public ValueTask<string> HandleAsync(MarkedCommand request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            internal sealed class MarkerDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, IInternalMarker
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Id == "DCTR014");
        result.Registrations.GetPipeline("global::MarkedCommand", "string").Should().Contain("MarkerDecorator");
    }

    [Fact]
    public void InternalDecoratorWithPublicConstraint_IsExported()
    {
        var (library, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly("""
                public interface IAudited;
                public sealed record AuditedCommand : IRequest, IAudited;

                public sealed class AuditedHandler : IRequestHandler<AuditedCommand, string>
                {
                    public ValueTask<string> HandleAsync(AuditedCommand request, CancellationToken cancellationToken = default) => default;
                }
                """ + "\n" + TestSources.InternalDecorator("AuditDecorator", 1, "IRequest, IAudited")),
            TestSources.EmptyHost());

        library.Diagnostics.Should().NotContain(d => d.Id == "DCTR014");
        host.ShouldCompile();
        host.Registrations.GetPipeline("global::AuditedCommand", "string").Should().Contain("ApplyAuditDecorator");
        host.Registrations.TryGetPipeline("global::TestCommand", "string").Should().BeNull();
    }
}
