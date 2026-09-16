using AwesomeAssertions;
using DecoratR.Generator.Model;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// Unusual but legal shapes of handlers, decorators, requests and responses. Every scenario must produce
/// compiling code or a diagnostic; nothing may be dropped silently.
/// </summary>
public class EdgeCaseTests : GeneratorTestBase
{
    [Fact]
    public void GenericRequestType_IsSupported()
    {
        var registrations = RunGenerator(TestSources.FullPath("""
            public sealed record Wrapper<T>(T Value) : IRequest;

            public sealed class WrapperHandler : IRequestHandler<Wrapper<int>, string>
            {
                public ValueTask<string> HandleAsync(Wrapper<int> request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::Wrapper<int>, string>)");
        registrations.GetPipeline("global::Wrapper<int>", "string").Should().Contain("global::LoggingDecorator<global::Wrapper<int>, string>");
    }

    [Fact]
    public void NestedRequestAndHandlerTypes_AreSupported()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public static class Outer
            {
                public sealed record Inner : IRequest;

                internal sealed class InnerHandler : IRequestHandler<Inner, string>
                {
                    public ValueTask<string> HandleAsync(Inner request, CancellationToken cancellationToken = default) => default;
                }
            }
            """ + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.Should().Contain("typeof(global::Outer.InnerHandler)");
        registrations.GetPipeline("global::Outer.Inner", "string").Should().Contain("LoggingDecorator");
    }

    [Fact]
    public void ExoticResponseTypes_AreSupported()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public sealed record A : IRequest;
            public sealed record B : IRequest;
            public sealed record C : IRequest;
            public sealed record D : IRequest;

            public sealed class TupleHandler : IRequestHandler<A, (int Count, string Name)>
            {
                public ValueTask<(int Count, string Name)> HandleAsync(A request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class ArrayHandler : IRequestHandler<B, string[]>
            {
                public ValueTask<string[]> HandleAsync(B request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class NullableHandler : IRequestHandler<C, int?>
            {
                public ValueTask<int?> HandleAsync(C request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class DictionaryHandler : IRequestHandler<D, IReadOnlyDictionary<string, List<int>>>
            {
                public ValueTask<IReadOnlyDictionary<string, List<int>>> HandleAsync(D request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.GetDecoratorSection().CountOccurrences("Decorate(services").Should().Be(4);
        registrations.Should().Contain("global::DecoratR.IRequestHandler<global::A, (int Count, string Name)>");
        registrations.Should().Contain("global::DecoratR.IRequestHandler<global::D, global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.List<int>>>");
    }

    [Fact]
    public void AbstractBaseHandler_OnlyDerivedIsRegistered()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            public sealed record Cmd : IRequest;

            public abstract class BaseHandler : IRequestHandler<Cmd, string>
            {
                public abstract ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default);
            }

            public sealed class DerivedHandler : BaseHandler
            {
                public override ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Id == "DCTR007");
        result.Registrations.CountOccurrences("services.Add(").Should().Be(1);
        result.Registrations.Should().Contain("typeof(global::DerivedHandler)");
    }

    [Fact]
    public void DecoratorInheritingFromBaseClass_IsSupported()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public abstract class DecoratorBase<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                protected IRequestHandler<TRequest, TResponse> Inner { get; } = inner;
                public abstract ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
            }

            [Decorator(Order = 1)]
            public sealed class LoggingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : DecoratorBase<TRequest, TResponse>(inner)
                where TRequest : IRequest
            {
                public override ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => Inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Severity >= DiagnosticSeverity.Warning);
        result.Registrations.GetPipeline("global::TestCommand", "string").Should().Contain("global::LoggingDecorator<global::TestCommand, string>");
    }

    [Fact]
    public void DecoratorImplementingDerivedInterface_IsSupported()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public interface IPipelineBehavior<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest;

            [Decorator(Order = 1)]
            public sealed class LoggingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Severity >= DiagnosticSeverity.Warning);
        result.Registrations.GetPipeline("global::TestCommand", "string").Should().Contain("LoggingDecorator");
    }

    [Fact]
    public void HandlerImplementingDerivedInterface_IsDiscovered()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, string> where TCommand : IRequest;

            public sealed record Cmd : IRequest;

            public sealed class CmdHandler : ICommandHandler<Cmd>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """)).ShouldCompile().Registrations;

        registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::Cmd, string>),");
        registrations.Should().Contain("typeof(global::CmdHandler),");
    }

    [Fact]
    public void DecoratorImplementingBothPipelines_IsDCTR004()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public sealed class Both<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>, IStreamRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, IStreamRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
                IAsyncEnumerable<TResponse> IStreamRequestHandler<TRequest, TResponse>.HandleAsync(TRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR004").GetMessage().Should().Contain("2 handler interfaces");
        result.Registrations.Should().NotContain("Both<");
    }

    [Fact]
    public void BothTriggerAttributes_ProduceRegistriesAndRegistrations()
    {
        var result = RunGenerator($"""
            using DecoratR;

            {TestSources.MetadataAttribute}
            {TestSources.RegistrationsAttribute}

            {TestSources.TestCommandRecord}

            {TestSources.TestCommandHandler}

            {TestSources.Decorator("LoggingDecorator", 1)}
            """).ShouldCompile();

        result.Sources.Keys.Should().BeEquivalentTo(
            GenerationResult.HandlerRegistryHint,
            GenerationResult.DecoratorRegistryHint,
            GenerationResult.RegistrationsHint);
        result.Registrations.Should().Contain("typeof(global::TestCommandHandler)");
        result.Registrations.Should().NotContain("DecoratRHandlerRegistry", "an assembly never registers its own handlers twice");
    }

    [Fact]
    public void FileLocalHandler_IsDCTR008()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            public sealed record Cmd : IRequest;

            file sealed class Hidden : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR008").GetMessage().Should().Contain("Hidden");
        result.Registrations.Should().NotContain("Hidden");
    }

    [Fact]
    public void NegativeOrder_IsOutermost()
    {
        var registrations = RunGenerator(TestSources.FullPath(
            TestSources.Decorator("ZeroDecorator", 0) + "\n" +
            TestSources.Decorator("NegativeDecorator", -5))).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::TestCommand", "string").GetPipelineSteps();
        steps[0].Should().Contain("ZeroDecorator").And.EndWith("// Order 0");
        steps[1].Should().Contain("NegativeDecorator").And.EndWith("// Order -5");
    }

    [Fact]
    public void BaseClassConstraint_MatchesDerivedRequestsOnly()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public abstract record RequestBase : IRequest;
            public sealed record Derived : RequestBase;
            public sealed record Unrelated : IRequest;

            public sealed class DerivedHandler : IRequestHandler<Derived, string>
            {
                public ValueTask<string> HandleAsync(Derived request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class UnrelatedHandler : IRequestHandler<Unrelated, string>
            {
                public ValueTask<string> HandleAsync(Unrelated request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.Decorator("BaseDecorator", 1, "RequestBase"))).ShouldCompile().Registrations;

        registrations.GetPipeline("global::Derived", "string").Should().Contain("BaseDecorator");
        registrations.TryGetPipeline("global::Unrelated", "string").Should().BeNull();
    }

    [Fact]
    public void RequestImplementingBothMarkers_HasSeparatePipelines()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            public sealed record Dual : IRequest, IStreamRequest;

            public sealed class DualRequestHandler : IRequestHandler<Dual, string>
            {
                public ValueTask<string> HandleAsync(Dual request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class DualStreamHandler : IStreamRequestHandler<Dual, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(Dual request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    await Task.Yield();
                    yield return "x";
                }
            }
            """ + "\n" + TestSources.Decorator("RegularDecorator", 1) + "\n" + TestSources.StreamDecorator("StreamDecorator", 1))).ShouldCompile();

        result.Diagnostics.Should().NotContain(d => d.Id == "DCTR007");
        result.Registrations.GetPipeline("global::Dual", "string").GetPipelineSteps().Should().ContainSingle().Which.Should().Contain("RegularDecorator");
        result.Registrations.GetPipeline("global::Dual", "string", isStream: true).GetPipelineSteps().Should().ContainSingle().Which.Should().Contain("StreamDecorator");
    }

    [Fact]
    public void NonGenericTypeWithDecoratorAttribute_IsNeitherDecoratorNorHandler()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public sealed class Confused(IRequestHandler<TestCommand, string> inner) : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostics.Should().ContainSingle(d => d.Id == "DCTR006");
        result.Diagnostics.Should().NotContain(d => d.Id == "DCTR007");
        result.Registrations.Should().NotContain("Confused");
    }

    [Fact]
    public void ObsoleteTypes_DoNotProduceWarningsInGeneratedCode()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            [Obsolete("old")]
            public sealed record OldCommand : IRequest;

            [Obsolete("old")]
            public sealed class OldHandler : IRequestHandler<OldCommand, string>
            {
                public ValueTask<string> HandleAsync(OldCommand request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile();

        result.OutputCompilation.GetDiagnostics(Ct)
            .Where(d => d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true)
            .Should().NotContain(d => d.Id == "CS0612" || d.Id == "CS0618");
    }

    [Fact]
    public void ReferencedNonPublicServiceType_WithoutApplicableDecorators_DoesNotWarn()
    {
        var (_, host) = RunTwoStageGenerator(
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            internal sealed record InternalCommand : IRequest;

            internal sealed class InternalHandler : IRequestHandler<InternalCommand, string>
            {
                public ValueTask<string> HandleAsync(InternalCommand request, CancellationToken cancellationToken = default) => default;
            }
            """,
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Diagnostics.Should().NotContain(d => d.Id == "DCTR011");
        host.Registrations.Should().NotContain("Skipped");
    }

    [Fact]
    public void KeywordNamespace_IsEscapedInGeneratedCode()
    {
        var registrations = RunGenerator($$"""
            using DecoratR;

            {{TestSources.RegistrationsAttribute}}

            namespace @event
            {
                public sealed record Cmd : IRequest;

                public sealed class CmdHandler : IRequestHandler<Cmd, string>
                {
                    public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
                }

                {{TestSources.Decorator("LoggingDecorator", 1)}}
            }
            """).ShouldCompile().Registrations;

        registrations.Should().Contain("typeof(global::@event.CmdHandler)");
        registrations.GetPipeline("global::@event.Cmd", "string").Should().Contain("global::@event.LoggingDecorator<global::@event.Cmd, string>");
    }

    [Fact]
    public void UnmanagedAndNotNullConstraints_AreMatched()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public readonly record struct Unmanaged(int Value) : IRequest;
            public sealed record Managed : IRequest;

            public sealed class UnmanagedHandler : IRequestHandler<Unmanaged, string>
            {
                public ValueTask<string> HandleAsync(Unmanaged request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class ManagedHandler : IRequestHandler<Managed, string>
            {
                public ValueTask<string> HandleAsync(Managed request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public sealed class UnmanagedDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : unmanaged, IRequest
                where TResponse : notnull
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile().Registrations;

        registrations.GetPipeline("global::Unmanaged", "string").Should().Contain("UnmanagedDecorator");
        registrations.TryGetPipeline("global::Managed", "string").Should().BeNull();
    }

    [Fact]
    public void SelfReferencingResponseConstraint_IsMatched()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public sealed record Count : IRequest;
            public sealed record Name : IRequest;
            public sealed class Opaque;

            public sealed class CountHandler : IRequestHandler<Count, int>
            {
                public ValueTask<int> HandleAsync(Count request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class NameHandler : IRequestHandler<Name, Opaque>
            {
                public ValueTask<Opaque> HandleAsync(Name request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public sealed class ComparableDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
                where TResponse : IComparable<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile().Registrations;

        registrations.GetPipeline("global::Count", "int").Should().Contain("ComparableDecorator");
        registrations.TryGetPipeline("global::Name", "global::Opaque").Should().BeNull();
    }

    [Fact]
    public void RequestNestedInGenericType_IsSupported()
    {
        var registrations = RunGenerator(TestSources.EmptyHost("""
            public static class Envelope<T>
            {
                public sealed record Request(T Payload) : IRequest;
            }

            public sealed class EnvelopeHandler : IRequestHandler<Envelope<int>.Request, string>
            {
                public ValueTask<string> HandleAsync(Envelope<int>.Request request, CancellationToken cancellationToken = default) => default;
            }
            """ + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile().Registrations;

        registrations.GetPipeline("global::Envelope<int>.Request", "string").Should().Contain("LoggingDecorator");
    }

    [Fact]
    public void NullableResponseInGenericConstraint_ProducesWarningFreeCode()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            public interface IQuery<TResult> : IRequest;
            public sealed record Product(string Name);

            public sealed record GetProduct(Guid Id) : IQuery<Product?>;
            public sealed record GetName : IQuery<string>;

            public sealed class GetProductHandler : IRequestHandler<GetProduct, Product?>
            {
                public ValueTask<Product?> HandleAsync(GetProduct request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class GetNameHandler : IRequestHandler<GetName, string>
            {
                public ValueTask<string> HandleAsync(GetName request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public sealed class QueryDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IQuery<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::GetProduct, global::Product?>)");
        result.Registrations.GetPipeline("global::GetProduct", "global::Product?").Should().Contain("global::QueryDecorator<global::GetProduct, global::Product?>");
        result.Registrations.GetPipeline("global::GetName", "string").Should().Contain("QueryDecorator");
        GeneratedWarnings(result).Should().BeEmpty();
    }

    [Fact]
    public void NullabilityMismatchBetweenRequestAndHandler_StillAppliesDecoratorWithoutWarnings()
    {
        var result = RunGenerator(TestSources.EmptyHost("""
            public interface IQuery<TResult> : IRequest;
            public sealed record Product(string Name);

            // The query promises a non-null Product but the handler returns Product?.
            public sealed record GetProduct(Guid Id) : IQuery<Product>;

            public sealed class GetProductHandler : IRequestHandler<GetProduct, Product?>
            {
                public ValueTask<Product?> HandleAsync(GetProduct request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public sealed class QueryDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IQuery<TResponse>
                where TResponse : notnull
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Registrations.GetPipeline("global::GetProduct", "global::Product?").Should().Contain("QueryDecorator");
        GeneratedWarnings(result).Should().BeEmpty();
    }

    [Fact]
    public void NullableClassConstraint_IsPreservedInApplyMethod()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            [Decorator(Order = 1)]
            public sealed class RefDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : class, IRequest
                where TResponse : class?
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """, "HandlerLib").ShouldCompile();

        var applyMethod = result.DecoratorRegistry.GetApplyMethod("ApplyRefDecorator");
        applyMethod.Should().Contain("where TRequest : class, global::DecoratR.IRequest");
        applyMethod.Should().Contain("where TResponse : class?");
        result.DecoratorRegistry.Should().Contain("ResponseConstraints = \"!class?\"");
        GeneratedWarnings(result).Should().BeEmpty();
    }

    private static IEnumerable<Diagnostic> GeneratedWarnings(GenerationResult result) =>
        result.OutputCompilation.GetDiagnostics(Ct)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning &&
                        d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true);

    [Fact]
    public void MissingAssemblyName_FallsBackToStableNamespace()
    {
        AssemblyInfo.Create(null).Namespace.Should().Be("DecoratRGenerated");
        AssemblyInfo.Create("  ").Namespace.Should().Be("DecoratRGenerated");
    }

    [Fact]
    public void TriggerAttributes_InSeparateFile_AreDetected()
    {
        var compilation = CreateCompilation(TestSources.TestCommandRecord + "\n" + TestSources.TestCommandHandler, "TestAssembly")
            .AddSyntaxTrees(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                "[assembly: DecoratR.GenerateDecoratRRegistrations]", path: "AssemblyInfo.cs", cancellationToken: Ct));

        var result = RunGenerator(compilation.AddSyntaxTrees(
            Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("global using DecoratR;", path: "Usings.cs", cancellationToken: Ct)));

        result.ShouldCompile();
        result.Registrations.Should().Contain("typeof(global::TestCommandHandler)");
    }
}
