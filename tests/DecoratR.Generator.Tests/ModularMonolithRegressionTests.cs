using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// Regression tests for the findings of <c>docs/modular-monolith-analysis.md</c> (probes P1–P6). Skipped tests assert the
/// target behavior; the phase that implements a finding removes the skip.
/// </summary>
public class ModularMonolithRegressionTests : GeneratorTestBase
{
    private const string QueryDecorator = """
        public interface IQuery<TResult> : IRequest;

        [Decorator(Order = 1)]
        public sealed class QueryDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
            where TRequest : IQuery<TResponse>
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => inner.HandleAsync(request, cancellationToken);
        }
        """;

    /// <summary>Module A with a decorator constrained to every request; <c>/*Scope*/</c> is replaced per test.</summary>
    private const string ModuleA = """
        using DecoratR;

        [assembly: DecoratR.GenerateDecoratRMetadata]

        namespace ModuleA;

        public sealed record ACommand : IRequest;

        internal sealed class ACommandHandler : IRequestHandler<ACommand, string>
        {
            public ValueTask<string> HandleAsync(ACommand request, CancellationToken cancellationToken = default) => new("a");
        }

        [Decorator(Order = 5/*Scope*/)]
        internal sealed class TransactionDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner)
            : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => inner.HandleAsync(request, cancellationToken);
        }
        """;

    private const string ModuleB = """
        using DecoratR;

        [assembly: DecoratR.GenerateDecoratRMetadata]

        namespace ModuleB;

        public sealed record BCommand : IRequest;

        internal sealed class BCommandHandler : IRequestHandler<BCommand, string>
        {
            public ValueTask<string> HandleAsync(BCommand request, CancellationToken cancellationToken = default) => new("b");
        }
        """;

    // ── MM4 · Probe P1 ──────────────────────────────────────────────────

    [Fact(Skip = "MM4 · Phase 2.2: DCTR016 (Response-Mismatch) ist noch nicht implementiert")]
    public void MM4_QueryResponseMismatch_ReportsDCTR016()
    {
        var result = RunGenerator(TestSources.FullPath(QueryDecorator + """

            public sealed record GetItems : IQuery<IReadOnlyList<int>>;

            public sealed class GetItemsHandler : IRequestHandler<GetItems, List<int>>
            {
                public ValueTask<List<int>> HandleAsync(GetItems request, CancellationToken cancellationToken = default) => new([]);
            }
            """)).ShouldCompile();

        result.Registrations.TryGetPipeline("global::GetItems", "global::System.Collections.Generic.List<int>").Should().BeNull();
        var diagnostic = result.Diagnostic("DCTR016");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("QueryDecorator").And.Contain("GetItemsHandler");
    }

    [Fact(Skip = "MM4 · Phase 2.2: DCTR016 (Response-Mismatch) ist noch nicht implementiert")]
    public void MM4_QueryResponseMismatchAcrossModules_ReportsDCTR016InCompositionRoot()
    {
        var host = Compose(
            TestSources.EmptyHost(),
            ("SharedKernel", "using DecoratR;\n[assembly: DecoratR.GenerateDecoratRMetadata]\n" + QueryDecorator),
            ("Catalog", """
                using DecoratR;

                [assembly: DecoratR.GenerateDecoratRMetadata]

                public sealed record ListProducts : IQuery<IReadOnlyList<string>>;

                internal sealed class ListProductsHandler : IRequestHandler<ListProducts, List<string>>
                {
                    public ValueTask<List<string>> HandleAsync(ListProducts request, CancellationToken cancellationToken = default) => new([]);
                }
                """)).ShouldCompile();

        host.Diagnostic("DCTR016").GetMessage().Should().Contain("QueryDecorator").And.Contain("ListProductsHandler");
    }

    // ── MM6 · Probes P2, P3 ─────────────────────────────────────────────

    [Fact(Skip = "MM6 · Phase 2.5: das '?' von Nullable<T> wird wie eine NRT-Annotation entfernt (falsches DCTR007)")]
    public void MM6_NullableValueTypeResponse_IsADistinctServiceType()
    {
        var result = RunGenerator(TestSources.FullPath("""

            public sealed record Lookup : IRequest;

            public sealed class IntLookupHandler : IRequestHandler<Lookup, int>
            {
                public ValueTask<int> HandleAsync(Lookup request, CancellationToken cancellationToken = default) => new(1);
            }

            public sealed class NullableIntLookupHandler : IRequestHandler<Lookup, int?>
            {
                public ValueTask<int?> HandleAsync(Lookup request, CancellationToken cancellationToken = default) => new(1);
            }
            """)).ShouldCompile();

        result.DiagnosticsWithId("DCTR007").Should().BeEmpty();
        result.Registrations.Should().Contain("typeof(global::IntLookupHandler)").And.Contain("typeof(global::NullableIntLookupHandler)");
    }

    [Fact(Skip = "MM6 · Phase 2.5: IQuery<int?> matcht einen Handler mit Response int, der generierte Code bricht mit CS0311")]
    public void MM6_NullableValueTypeQuery_WithNonNullableResponse_IsNotDecorated()
    {
        var result = RunGenerator(TestSources.FullPath(QueryDecorator + """

            public sealed record FindCount : IQuery<int?>;

            public sealed class FindCountHandler : IRequestHandler<FindCount, int>
            {
                public ValueTask<int> HandleAsync(FindCount request, CancellationToken cancellationToken = default) => new(1);
            }
            """)).ShouldCompile();

        result.Registrations.TryGetPipeline("global::FindCount", "int").Should().BeNull();
    }

    // ── MM3 · Probe P4 ──────────────────────────────────────────────────

    [Fact(Skip = "MM3 · Phase 2.1: DCTR015 (fehlender Trigger) ist noch nicht implementiert")]
    public void MM3_ModuleWithHandlersButWithoutTrigger_ReportsDCTR015()
    {
        var module = RunGenerator("""
            using DecoratR;

            namespace Orders;

            public sealed record PlaceOrder : IRequest;

            internal sealed class PlaceOrderHandler : IRequestHandler<PlaceOrder, Guid>
            {
                public ValueTask<Guid> HandleAsync(PlaceOrder request, CancellationToken cancellationToken = default) => new(Guid.Empty);
            }
            """, "Orders.Application").ShouldCompile();

        var diagnostic = module.Diagnostic("DCTR015");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("Orders.Application");
    }

    // ── MM2 · Probe P5 ──────────────────────────────────────────────────

    /// <summary>
    /// Today every decorator is global: a decorator of module A also wraps the handlers of module B. This stays the
    /// behavior of <c>DecoratorScope.Global</c>; the scoped variant is covered by the skipped test below.
    /// </summary>
    [Fact]
    public void MM2_GlobalDecoratorOfOneModule_AppliesToHandlersOfOtherModules()
    {
        var host = Compose(TestSources.EmptyHost(), ("ModuleA", ModuleA), ("ModuleB", ModuleB)).ShouldCompile();

        host.Registrations.GetPipeline("global::ModuleB.BCommand", "string").GetPipelineSteps().Should().ContainSingle()
            .Which.Should().Contain("global::ModuleA.DecoratRDecoratorRegistry.ApplyModuleA_TransactionDecorator<global::ModuleB.BCommand, string>");
    }

    [Fact(Skip = "MM2 · Phase 2.3: DecoratorScope existiert noch nicht (API-Entscheidung offen, Analyse Abschnitt 8.3)")]
    public void MM2_AssemblyScopedDecorator_DoesNotApplyToOtherModules()
    {
        var host = Compose(
            TestSources.EmptyHost(),
            ("ModuleA", ModuleA.Replace("/*Scope*/", ", Scope = DecoratorScope.Assembly", StringComparison.Ordinal)),
            ("ModuleB", ModuleB)).ShouldCompile();

        host.Registrations.GetPipeline("global::ModuleA.ACommand", "string").GetPipelineSteps().Should().ContainSingle();
        host.Registrations.TryGetPipeline("global::ModuleB.BCommand", "string").Should().BeNull();
    }

    // ── MM5 · Probe P6 ──────────────────────────────────────────────────

    [Fact(Skip = "MM5 · Phase 2.4: AddDecoratR() ist public, zwei sichtbare Composition Roots führen zu CS0121")]
    public void MM5_CompositionRootReferencingAnotherCompositionRoot_CompilesAddDecoratRCall()
    {
        var host = Compose(
            """
            using DecoratR;
            using Microsoft.Extensions.DependencyInjection;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record WorkerCommand : IRequest;

            public sealed class WorkerCommandHandler : IRequestHandler<WorkerCommand, string>
            {
                public ValueTask<string> HandleAsync(WorkerCommand request, CancellationToken cancellationToken = default) => new("w");
            }

            public static class Startup
            {
                public static IServiceCollection Configure(IServiceCollection services) => services.AddDecoratR();
            }
            """,
            ("ApiHost", TestSources.FullPath()));

        host.ShouldCompile();
    }

    /// <summary>
    /// Compiles <paramref name="libraries"/> in order (each referencing the previous ones) and a host named
    /// <c>Host</c> that references all of them.
    /// </summary>
    private static GenerationResult Compose(string hostSource, params (string AssemblyName, string Source)[] libraries)
    {
        var references = DefaultReferences();
        foreach (var (assemblyName, source) in libraries)
        {
            var library = RunGenerator(CreateCompilation(source, assemblyName, references)).ShouldCompile();
            references = references.Add(library.OutputCompilation.ToMetadataReference());
        }

        return RunGenerator(CreateCompilation(hostSource, "Host", references));
    }
}
