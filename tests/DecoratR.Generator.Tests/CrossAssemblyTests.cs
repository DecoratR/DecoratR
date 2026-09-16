using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class CrossAssemblyTests : GeneratorTestBase
{
    [Fact]
    public void CompositionRoot_IteratesReferencedHandlerRegistry()
    {
        var (_, host) = RunTwoStageGenerator(TestSources.HandlerOnly(), TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.Should().Contain("// Handlers from referenced assemblies");
        host.Registrations.Should().Contain("foreach (var handler in global::HandlerLib.DecoratRHandlerRegistry.Handlers)");
        host.Registrations.Should().Contain("services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(handler.ServiceType, handler.ImplementationType, options.Lifetime));");
    }

    [Fact]
    public void InternalHandlerInLibrary_NotDirectlyReferencedByCompositionRoot()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record TestCommand(string Name) : IRequest;

            internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """,
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.Should().Contain("global::HandlerLib.DecoratRHandlerRegistry.Handlers");
        host.Registrations.Should().NotContain("InternalHandler");
    }

    [Fact]
    public void LocalDecorators_AppliedToReferencedServiceTypes()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(),
            TestSources.EmptyHost(TestSources.Decorator("LoggingDecorator", 1)));

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps().Should().ContainSingle()
            .Which.Should().Be("descriptor = Wrap<global::DecoratR.IRequestHandler<global::TestCommand, string>, global::LoggingDecorator<global::TestCommand, string>>(descriptor); // Order 1");
    }

    [Fact]
    public void MixedLocalAndReferencedHandlers_BothRegistered()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record RemoteCommand(string Name) : IRequest;

            public sealed class RemoteHandler : IRequestHandler<RemoteCommand, string>
            {
                public ValueTask<string> HandleAsync(RemoteCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Remote");
            }
            """,
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record LocalCommand(string Name) : IRequest;

            public sealed class LocalHandler : IRequestHandler<LocalCommand, string>
            {
                public ValueTask<string> HandleAsync(LocalCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Local");
            }
            """);

        host.ShouldCompile();
        host.Registrations.Should().Contain("global::HandlerLib.DecoratRHandlerRegistry.Handlers");
        host.Registrations.Should().Contain("typeof(global::LocalHandler)");
        host.Registrations.Should().Contain("// Handlers declared in this assembly");
        host.Registrations.Should().Contain("// Handlers from referenced assemblies");
        host.Diagnostic("DCTR002").GetMessage().Should().Contain("2 handler(s)");
    }

    [Fact]
    public void DecoratorInHandlerLib_AppliedViaApplyMethod()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps().Should().ContainSingle()
            .Which.Should().Be("descriptor = global::HandlerLib.DecoratRDecoratorRegistry.ApplyAppDecorator<global::TestCommand, string>(descriptor); // Order 1");
    }

    [Fact]
    public void DecoratorInHandlerLib_EmitsDecoratorAttribute()
    {
        var result = RunGenerator(TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 5)), "HandlerLib").ShouldCompile();

        result.DecoratorRegistry.Should().Contain(
            "[assembly: global::DecoratR.Metadata.DecoratRDecorator(\"global::HandlerLib.DecoratRDecoratorRegistry.ApplyAppDecorator\", \"global::AppDecorator\", 5, false, RequestConstraints = \"global::DecoratR.IRequest\")]");
    }

    [Fact]
    public void MixedLocalAndReferencedDecorators_OrderedGlobally()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            TestSources.EmptyHost(TestSources.Decorator("LocalDecorator", 2)));

        host.ShouldCompile();
        var steps = host.Registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps();
        steps[0].Should().Contain("LocalDecorator", "Order 2 is innermost and applied first");
        steps[1].Should().Contain("ApplyAppDecorator", "Order 1 is outermost and applied last");
    }

    [Fact]
    public void SameOrder_LocalAndReferencedDecorators_TieBreakByTypeName()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            namespace Zzz;

            [Decorator(Order = 1)]
            public sealed class Dec<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """,
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            namespace Aaa;

            public sealed record Cmd : IRequest;

            public sealed class Handler : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public sealed class Dec<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """);

        host.ShouldCompile();
        var steps = host.Registrations.GetPipeline("global::Aaa.Cmd", "string").GetPipelineSteps();
        steps[0].Should().Contain("ApplyZzz_Dec", "Zzz.Dec sorts after Aaa.Dec, so it is innermost and applied first");
        steps[1].Should().Contain("global::Aaa.Dec<", "Aaa.Dec sorts first, so it is outermost and applied last");
    }

    [Fact]
    public void ReferencedDecorator_AppliedToBothLocalAndRemoteHandlers()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record RemoteCommand(string Name) : IRequest;

            public sealed class RemoteHandler : IRequestHandler<RemoteCommand, string>
            {
                public ValueTask<string> HandleAsync(RemoteCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Remote");
            }

            [Decorator(Order = 1)]
            public class AppDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """,
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record LocalCommand(string Name) : IRequest;

            public sealed class LocalHandler : IRequestHandler<LocalCommand, string>
            {
                public ValueTask<string> HandleAsync(LocalCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Local");
            }
            """);

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::LocalCommand", "string").Should().Contain("ApplyAppDecorator");
        host.Registrations.GetPipeline("global::RemoteCommand", "string").Should().Contain("ApplyAppDecorator");
    }

    [Fact]
    public void DiagnosticCount_IncludesBothLocalAndReferencedDecorators()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            TestSources.EmptyHost(TestSources.Decorator("LocalDecorator", 2)));

        host.Diagnostic("DCTR003").GetMessage().Should().Contain("2 decorator(s)");
    }

    [Fact]
    public void HandlerLibWithoutDecorators_EmitsNoDecoratorRegistry()
    {
        var result = RunGenerator(TestSources.HandlerOnly(), "HandlerLib").ShouldCompile();

        result.Has(GenerationResult.DecoratorRegistryHint).Should().BeFalse();
        result.HandlerRegistry.Should().NotContain("DecoratRDecorator(");
    }

    [Fact]
    public void DecoratorOnlyLib_DiscoveredByCompositionRoot()
    {
        var (_, host) = RunTwoStageGenerator(
            $"""
             using DecoratR;

             {TestSources.MetadataAttribute}

             {TestSources.Decorator("FooDecorator", 2)}
             """,
            TestSources.FullPath());

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::TestCommand", "string").Should().Contain("ApplyFooDecorator");
        host.Registrations.Should().NotContain("// Handlers from referenced assemblies");
    }

    [Fact]
    public void InternalDecorator_AppliedViaGeneratedMethodNotDirectReference()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.InternalDecorator("InternalDecorator", 1)),
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.Should().Contain("ApplyInternalDecorator<global::TestCommand, string>(descriptor)");
        host.Registrations.Should().NotContain("global::InternalDecorator<");
    }

    [Fact]
    public void DecoratorRegistry_ContainsApplyMethodAndRuntimeHelpers()
    {
        var registry = RunGenerator(TestSources.HandlerOnly(TestSources.Decorator("LoggingDecorator", 1)), "HandlerLib").ShouldCompile().DecoratorRegistry;

        registry.Should().Contain("public static global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor ApplyLoggingDecorator<TRequest, TResponse>(");
        registry.Should().Contain("return Wrap<global::DecoratR.IRequestHandler<TRequest, TResponse>, global::LoggingDecorator<TRequest, TResponse>>(inner);");
        registry.Should().Contain("private static global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor Wrap<TService, [");
        registry.Should().Contain("DynamicallyAccessedMembers");
        registry.Should().Contain("CreateInnerFactory(");
        registry.Should().NotContain("Decorate(", "libraries only wrap descriptors; the composition root owns the service collection");
    }

    [Fact]
    public void DecoratorsWithCollidingApplyNames_GetUniqueSuffix()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            namespace A.B
            {
                [Decorator(Order = 1)]
                public sealed class C<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                    where TRequest : IRequest
                {
                    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
                }
            }

            namespace A_B
            {
                [Decorator(Order = 2)]
                public sealed class C<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                    where TRequest : IRequest
                {
                    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
                }
            }
            """, "HandlerLib").ShouldCompile();

        result.DecoratorRegistry.Should().Contain(" ApplyA_B_C<TRequest, TResponse>(");
        result.DecoratorRegistry.Should().Contain(" ApplyA_B_C_2<TRequest, TResponse>(");
    }

    [Fact]
    public void InternalRequestTypeInLibrary_IsSkippedInHostWithWarning()
    {
        var (library, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            internal sealed record InternalCommand : IRequest;

            internal sealed class InternalHandler : IRequestHandler<InternalCommand, string>
            {
                public ValueTask<string> HandleAsync(InternalCommand request, CancellationToken cancellationToken = default) => default;
            }
            """,
            TestSources.EmptyHost(TestSources.Decorator("LoggingDecorator", 1)));

        library.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR013");

        host.ShouldCompile();
        host.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR011").Which.GetMessage().Should().Contain("InternalCommand");
        host.Registrations.Should().NotContain("Decorate(services");
        host.Registrations.Should().Contain("// Skipped: decorators for global::DecoratR.IRequestHandler<global::InternalCommand, string>");
    }
}
