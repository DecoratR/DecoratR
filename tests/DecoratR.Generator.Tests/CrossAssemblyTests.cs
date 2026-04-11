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
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("HandlerLib.DecoratRHandlerRegistry.Handlers");
        registrations.Should().Contain("// Register handlers from referenced assemblies");
    }

    [Fact]
    public void InternalHandlerInLibrary_NotDirectlyReferencedByCompositionRoot()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
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
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("HandlerLib.DecoratRHandlerRegistry.Handlers");
        registrations.Should().NotContain("InternalHandler");
    }

    [Fact]
    public void Decorators_AppliedUsingReferencedServiceTypes()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}

             {TestSources.Decorator("LoggingDecorator", 1)}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("DecorateService<");
        registrations.Should().Contain("LoggingDecorator");
        registrations.Should().Contain("TestCommand");
    }

    [Fact]
    public void MixedLocalAndReferencedHandlers_BothRegistered()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
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

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("HandlerLib.DecoratRHandlerRegistry.Handlers");
        registrations.Should().Contain("LocalHandler");
        registrations.Should().Contain("// Register local handlers");
        registrations.Should().Contain("// Register handlers from referenced assemblies");
    }

    [Fact]
    public void DecoratorInHandlerLib_DiscoveredViaApplyMethod()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyAppDecorator");
        registrations.Should().Contain("TestCommand");
    }

    [Fact]
    public void DecoratorInHandlerLib_EmitsDecoratorRegistrationAttribute()
    {
        var source = TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 5));

        var (_, generatedTrees) = RunGenerator(source, "HandlerLib");

        var registrations = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        registrations.Should().Contain("[assembly: global::DecoratR.DecoratRDecoratorRegistration(");
        registrations.Should().Contain("ApplyAppDecorator");
        registrations.Should().Contain(", 5)]");
    }

    [Fact]
    public void MixedLocalAndReferencedDecorators_OrderedGlobally()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}

             {TestSources.Decorator("LocalDecorator", 2)}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyAppDecorator");
        registrations.Should().Contain("LocalDecorator");

        var decorateSection = registrations.GetDecoratorApplicationSection();
        var localIdx = decorateSection.IndexOf("LocalDecorator", StringComparison.Ordinal);
        var appIdx = decorateSection.IndexOf("ApplyAppDecorator", StringComparison.Ordinal);
        localIdx.Should().BeLessThan(appIdx,
            "because LocalDecorator (Order=2, innermost) should be registered before AppDecorator (Order=1, outermost)");
    }

    [Fact]
    public void ReferencedDecorator_AppliedToBothLocalAndRemoteHandlers()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
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
            public class AppDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                private readonly IRequestHandler<TRequest, TResponse> _inner;
                public AppDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => _inner.HandleAsync(request, cancellationToken);
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

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyAppDecorator");

        var decorateSection = registrations.GetDecoratorApplicationSection();
        decorateSection.Should().Contain("LocalCommand");
        decorateSection.Should().Contain("RemoteCommand");
    }

    [Fact]
    public void DiagnosticCount_IncludesBothLocalAndReferencedDecorators()
    {
        var (diagnostics, _) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}

             {TestSources.Decorator("LocalDecorator", 2)}
             """);

        var decoratorDiag = diagnostics.FirstOrDefault(d => d.Id == "DCTR003");
        decoratorDiag.Should().NotBeNull();
        decoratorDiag!.GetMessage().Should().Contain("2");
    }

    [Fact]
    public void OnlyReferencedDecorators_AppliedViaGeneratedMethod()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.Decorator("AppDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyAppDecorator");
    }

    [Fact]
    public void HandlerLibWithoutDecorators_EmitsNoDecoratorAttributes()
    {
        var source = TestSources.HandlerOnly();

        var (_, generatedTrees) = RunGenerator(source, "HandlerLib");

        var registrations = generatedTrees.FindSource("DecoratRHandlerRegistry");
        registrations.Should().NotContain("[assembly: global::DecoratR.DecoratRDecoratorRegistration(");
        generatedTrees.Should().NotContain(t => t.Contains("DecoratRDecoratorRegistry"));
    }

    [Fact]
    public void DecoratorOnlyLib_DiscoveredByCompositionRoot()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            $"""
             using DecoratR;

             {TestSources.MetadataAttribute}

             {TestSources.Decorator("FooDecorator", 2)}
             """,
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}

             {TestSources.TestCommandRecord}

             {TestSources.TestCommandHandler}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyFooDecorator");
        registrations.Should().Contain("TestCommand");
    }

    [Fact]
    public void InternalDecorator_AppliedViaGeneratedMethodNotDirectReference()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            TestSources.HandlerOnly(TestSources.InternalDecorator("InternalDecorator", 1)),
            $"""
             using DecoratR;

             {TestSources.RegistrationsAttribute}
             """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        registrations.Should().Contain("ApplyInternalDecorator");
        registrations.Should().NotContain("global::InternalDecorator<");
    }

    [Fact]
    public void DecoratorRegistry_ContainsApplyMethodAndDecorateServiceCore()
    {
        var source = TestSources.HandlerOnly(TestSources.Decorator("LoggingDecorator", 1));

        var (_, generatedTrees) = RunGenerator(source, "HandlerLib");

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");
        decoratorRegistry.Should().Contain("public static void ApplyLoggingDecorator<TRequest, TResponse>");
        decoratorRegistry.Should().Contain("IServiceCollection services");
        decoratorRegistry.Should().Contain("private static void DecorateService<TRequest, TResponse,");
        decoratorRegistry.Should().Contain("DynamicallyAccessedMembers");
        decoratorRegistry.Should().Contain("LoggingDecorator<TRequest, TResponse>");
        decoratorRegistry.Should()
            .Contain("DecorateService<TRequest, TResponse, global::LoggingDecorator<TRequest, TResponse>>(services)");
    }
}