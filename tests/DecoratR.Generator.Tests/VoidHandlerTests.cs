using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// Handlers without a response implement <c>IRequestHandler&lt;TRequest&gt;</c>. They are registered as
/// <c>IRequestHandler&lt;TRequest, Unit&gt;</c> (the decorated pipeline) plus a <c>VoidRequestHandler</c> facade
/// for <c>IRequestHandler&lt;TRequest&gt;</c>.
/// </summary>
public class VoidHandlerTests : GeneratorTestBase
{
    private const string UnitService = "global::DecoratR.IRequestHandler<global::DeleteCommand, global::DecoratR.Unit>";
    private const string FacadeService = "global::DecoratR.IRequestHandler<global::DeleteCommand>";
    private const string FacadeImplementation = "global::DecoratR.VoidRequestHandler<global::DeleteCommand>";

    private const string VoidCommand = """
        public sealed record DeleteCommand(string Id) : IRequest;

        public sealed class DeleteCommandHandler : IRequestHandler<DeleteCommand>
        {
            public ValueTask HandleAsync(DeleteCommand request, CancellationToken cancellationToken = default) => default;
        }
        """;

    [Fact]
    public void Host_RegistersUnitServiceAndFacade()
    {
        var result = RunGenerator(TestSources.FullPath(VoidCommand)).ShouldCompile();

        result.Registrations.Should().Contain($"typeof({UnitService})");
        result.Registrations.Should().Contain("typeof(global::DeleteCommandHandler)");
        result.Registrations.Should().Contain($"typeof({FacadeService})");
        result.Registrations.Should().Contain($"typeof({FacadeImplementation})");
        result.Diagnostic("DCTR002").GetMessage().Should().Contain("2 handler(s)");
    }

    [Fact]
    public void Library_RegistryContainsFacade_MetadataDescribesUnitServiceOnly()
    {
        var result = RunGenerator(TestSources.HandlerOnly(VoidCommand)).ShouldCompile();

        result.HandlerRegistry.Should().Contain($"new(typeof({UnitService}), typeof(global::DeleteCommandHandler))");
        result.HandlerRegistry.Should().Contain($"new(typeof({FacadeService}), typeof({FacadeImplementation}))");
        result.HandlerRegistry.Should().Contain(
            "DecoratRHandler(\"global::DeleteCommandHandler\", \"global::DeleteCommand\", \"global::DecoratR.Unit\", false");
        result.HandlerRegistry.CountOccurrences("VoidRequestHandler").Should().Be(1);
    }

    [Fact]
    public void Decorators_WrapTheUnitPipeline_NotTheFacade()
    {
        var result = RunGenerator(TestSources.FullPath(VoidCommand + "\n" + TestSources.Decorator("LoggingDecorator", 1))).ShouldCompile();

        result.Registrations.GetPipeline("global::DeleteCommand", "global::DecoratR.Unit")
            .Should().Contain("global::LoggingDecorator<global::DeleteCommand, global::DecoratR.Unit>");
        result.Registrations.Should().NotContain($"Decorate(services, typeof({FacadeService})");
    }

    [Fact]
    public void ResponseConstraints_TreatUnitAsStruct()
    {
        var result = RunGenerator(TestSources.FullPath(
            VoidCommand + "\n" +
            TestSources.Decorator("ClassOnlyDecorator", 1, responseConstraint: "class") + "\n" +
            TestSources.Decorator("StructOnlyDecorator", 2, responseConstraint: "struct"))).ShouldCompile();

        var pipeline = result.Registrations.GetPipeline("global::DeleteCommand", "global::DecoratR.Unit");
        pipeline.Should().Contain("global::StructOnlyDecorator<global::DeleteCommand, global::DecoratR.Unit>");
        pipeline.Should().NotContain("ClassOnlyDecorator");
    }

    [Fact]
    public void CrossAssembly_HostDecoratesLibraryUnitPipeline_FacadeComesFromRegistry()
    {
        var (library, host) = RunTwoStageGenerator(
            TestSources.HandlerOnly(VoidCommand),
            TestSources.EmptyHost(TestSources.Decorator("HostDecorator", 1)));
        host.ShouldCompile();

        library.HandlerRegistry.Should().Contain(FacadeImplementation);
        host.Registrations.Should().Contain("global::HandlerLib.DecoratRHandlerRegistry.Handlers");
        host.Registrations.GetPipeline("global::DeleteCommand", "global::DecoratR.Unit")
            .Should().Contain("global::HostDecorator<global::DeleteCommand, global::DecoratR.Unit>");
        host.Registrations.Should().NotContain("VoidRequestHandler");
    }

    [Fact]
    public void HandlerImplementingUnitInterfaceDirectly_GetsNoFacade()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public sealed record DeleteCommand(string Id) : IRequest;

            public sealed class DeleteCommandHandler : IRequestHandler<DeleteCommand, Unit>
            {
                public ValueTask<Unit> HandleAsync(DeleteCommand request, CancellationToken cancellationToken = default) => default;
            }
            """)).ShouldCompile();

        result.Registrations.Should().Contain($"typeof({UnitService})");
        result.Registrations.Should().NotContain("VoidRequestHandler");
    }

    [Fact]
    public void HandlerWithVoidAndResponseInterfaces_RegistersFacadeOnlyForUnitService()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public sealed record DeleteCommand(string Id) : IRequest;

            public sealed class DualHandler : IRequestHandler<DeleteCommand>, IRequestHandler<DeleteCommand, string>
            {
                public ValueTask HandleAsync(DeleteCommand request, CancellationToken cancellationToken = default) => default;
                ValueTask<string> IRequestHandler<DeleteCommand, string>.HandleAsync(DeleteCommand request, CancellationToken cancellationToken) => default;
            }
            """)).ShouldCompile();

        result.Registrations.Should().Contain("typeof(global::DecoratR.IRequestHandler<global::DeleteCommand, string>)");
        result.Registrations.CountOccurrences("VoidRequestHandler").Should().Be(1);
    }

    [Fact]
    public void TwoVoidHandlersForSameRequest_ReportDCTR007()
    {
        var result = RunGenerator(TestSources.FullPath(VoidCommand + """

            public sealed class SecondDeleteHandler : IRequestHandler<DeleteCommand>
            {
                public ValueTask HandleAsync(DeleteCommand request, CancellationToken cancellationToken = default) => default;
            }
            """));

        var diagnostic = result.Diagnostic("DCTR007");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("IRequestHandler<DeleteCommand, DecoratR.Unit>");
    }

    [Fact]
    public void DecoratorOnVoidInterface_ReportsDCTR005WithHint()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public sealed class VoidDecorator<TRequest>(IRequestHandler<TRequest> inner) : IRequestHandler<TRequest>
                where TRequest : IRequest
            {
                public ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        var diagnostic = result.Diagnostic("DCTR005");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("without a response");
        result.Registrations.Should().NotContain("VoidDecorator");
    }

    [Fact]
    public void RegularDecoratorMismatch_HasNoVoidHint()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public class Three<TRequest, TResponse, TExtra>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Diagnostic("DCTR005").GetMessage().Should().EndWith("handler interface");
    }
}
