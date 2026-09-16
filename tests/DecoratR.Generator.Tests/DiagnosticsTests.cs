using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

public class DiagnosticsTests : GeneratorTestBase
{
    [Fact]
    public void DCTR004_DecoratorWithoutHandlerInterface_IsErrorAndSkipped()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class NoInterface<TRequest, TResponse> where TRequest : IRequest { }
            """)).ShouldCompile();

        var diagnostic = result.Diagnostic("DCTR004");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("NoInterface<TRequest, TResponse>").And.Contain("none");
        result.LocationText(diagnostic).Should().Be("NoInterface");
        result.Registrations.Should().NotContain("NoInterface");
    }

    [Fact]
    public void DCTR005_DecoratorWithThreeTypeParameters_IsError()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class Three<TRequest, TResponse, TExtra>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR005").Severity.Should().Be(DiagnosticSeverity.Error);
        result.Registrations.Should().NotContain("Three<");
    }

    [Fact]
    public void DCTR005_DecoratorWithClosedHandlerInterface_IsError()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class Closed<T1, T2>(IRequestHandler<TestCommand, string> inner) : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR005");
    }

    [Fact]
    public void SwappedTypeParameters_AreSupported()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class Swapped<TResponse, TRequest>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.Registrations.GetPipeline("global::TestCommand", "string").Should().Contain("global::Swapped<string, global::TestCommand>");
    }

    [Theory]
    [InlineData("public class NotGeneric(IRequestHandler<TestCommand, string> inner) : IRequestHandler<TestCommand, string> { public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken); }", "is not an open generic type")]
    [InlineData("public abstract class Abstract<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest { public abstract ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default); }", "is abstract")]
    [InlineData("public static class Static<TRequest, TResponse> { }", "is static")]
    public void DCTR006_UnusableDecorator_IsWarningWithReason(string declaration, string reason)
    {
        var result = RunGenerator(TestSources.FullPath($"""
            [Decorator(Order = 1)]
            {declaration}
            """)).ShouldCompile();

        var diagnostic = result.Diagnostic("DCTR006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain(reason);
        result.Registrations.GetPipelineStepsOrEmpty().Should().BeEmpty();
    }

    [Fact]
    public void DCTR006_DecoratorNestedInGenericType_IsWarning()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public static class Outer<T>
            {
                [Decorator(Order = 1)]
                public class Nested<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                    where TRequest : IRequest
                {
                    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
                }
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR006").GetMessage().Should().Contain("nested in a generic type");
    }

    [Fact]
    public void DCTR007_TwoHandlersForSameServiceType_IsError()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public sealed class SecondHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => default;
            }
            """));

        var diagnostic = result.Diagnostic("DCTR007");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("IRequestHandler<TestCommand, string>").And.Contain("SecondHandler").And.Contain("TestCommandHandler");
        result.LocationText(diagnostic).Should().EndWith("Handler");
    }

    [Fact]
    public void DCTR007_DerivedHandlerOfConcreteBase_IsError()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public class BaseHandler : IRequestHandler<TestQuery, int>
            {
                public virtual ValueTask<int> HandleAsync(TestQuery request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class DerivedHandler : BaseHandler;
            """ + "\n" + TestSources.TestQueryRecord));

        result.Diagnostic("DCTR007").GetMessage().Should().Contain("BaseHandler").And.Contain("DerivedHandler");
    }

    [Fact]
    public void DCTR007_LocalHandlerCollidingWithReferencedHandler_IsError()
    {
        var (_, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(),
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed class LocalDuplicate : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => default;
            }
            """);

        host.Diagnostic("DCTR007").GetMessage().Should().Contain("LocalDuplicate").And.Contain("TestCommandHandler");
    }

    [Fact]
    public void DCTR008_PrivateNestedHandler_IsWarningAndSkipped()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public static class Outer
            {
                private sealed class Hidden : IRequestHandler<TestQuery, int>
                {
                    public ValueTask<int> HandleAsync(TestQuery request, CancellationToken cancellationToken = default) => default;
                }
            }
            """ + "\n" + TestSources.TestQueryRecord)).ShouldCompile();

        result.Diagnostic("DCTR008").GetMessage().Should().Contain("Outer.Hidden");
        result.Registrations.Should().NotContain("Hidden");
    }

    [Fact]
    public void DCTR008_PrivateNestedDecorator_IsWarningAndSkipped()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public static class Outer
            {
                [Decorator(Order = 1)]
                private sealed class Hidden<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                    where TRequest : IRequest
                {
                    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
                }
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR008").GetMessage().Should().Contain("Hidden<TRequest, TResponse>");
        result.Registrations.Should().NotContain("Hidden<");
    }

    [Fact]
    public void DCTR009_HostWithoutDependencyInjectionAbstractions_IsError()
    {
        var result = RunGeneratorWithoutDependencyInjection("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]
            """);

        var diagnostic = result.Diagnostic("DCTR009");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        result.LocationText(diagnostic).Should().Contain("GenerateDecoratRRegistrations");
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public void DCTR010_StructHandler_IsWarningAndSkipped()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public readonly struct StructHandler : IRequestHandler<TestQuery, int>
            {
                public ValueTask<int> HandleAsync(TestQuery request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.TestQueryRecord)).ShouldCompile();

        result.Diagnostic("DCTR010").GetMessage().Should().Contain("StructHandler");
        result.Registrations.Should().NotContain("StructHandler");
    }

    [Fact]
    public void DCTR012_DecoratorWithoutInnerConstructor_IsWarningButStillApplied()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class NoCtor<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => default;
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR012").GetMessage().Should().Contain("NoCtor<TRequest, TResponse>").And.Contain("IRequestHandler<TRequest, TResponse>");
        result.Registrations.Should().Contain("global::NoCtor<global::TestCommand, string>");
    }

    [Fact]
    public void DiscoveryDiagnostics_AreHidden()
    {
        var result = RunGenerator(TestSources.FullPath(TestSources.Decorator("LoggingDecorator", 1)));

        result.Diagnostic("DCTR002").Severity.Should().Be(DiagnosticSeverity.Hidden);
        result.Diagnostic("DCTR003").Severity.Should().Be(DiagnosticSeverity.Hidden);
    }

    [Fact]
    public void AllDescriptors_HaveHelpLinks()
    {
        var descriptors = typeof(Diagnostics).GetFields()
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToList();

        descriptors.Should().HaveCountGreaterThan(10);
        descriptors.Should().OnlyContain(d => d.HelpLinkUri.EndsWith(d.Id.ToLowerInvariant()));
        descriptors.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }
}

file static class Extensions
{
    public static IReadOnlyList<string> GetPipelineStepsOrEmpty(this string registrations) =>
        registrations.Contains("// Decorator pipelines") ? registrations.GetDecoratorSection().GetPipelineSteps() : [];
}
