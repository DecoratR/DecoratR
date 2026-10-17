using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// Generated factories call handler and decorator constructors directly; constructors the generator cannot map 1:1
/// fall back to <c>ActivatorUtilities</c> (registration by implementation type, <c>Wrap</c> for decorators).
/// </summary>
public class DirectConstructionTests : GeneratorTestBase
{
    private const string GetRequiredService = "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService";
    private const string GetRequiredKeyedService = "global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService";

    private static GenerationResult RunHost(string handler) => RunGenerator($$"""
        using DecoratR;
        using Microsoft.Extensions.DependencyInjection;

        {{TestSources.RegistrationsAttribute}}

        public interface IStore;
        public enum Region { Europe = 1, America = -2 }
        public sealed record Cmd : IRequest;

        {{handler}}
        """).ShouldCompile();

    private static string Factory(GenerationResult result) =>
        result.Registrations.GetSectionBetween("// Handlers declared in this assembly", "lifetime));");

    private static void ShouldBeRegisteredByImplementationType(GenerationResult result) =>
        result.Registrations.Should().Contain(
            "services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(global::DecoratR.IRequestHandler<global::Cmd, string>), typeof(global::Handler), lifetime));");

    [Fact]
    public void PlainServicesAndServiceProvider_AreResolvedInParameterOrder()
    {
        var result = RunHost("""
            public sealed class Handler(IStore store, IServiceProvider services, IEnumerable<IStore> all) : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """);

        Factory(result).Should().Contain($$"""
                        (global::System.Func<global::System.IServiceProvider, global::DecoratR.IRequestHandler<global::Cmd, string>>)(static sp => new global::Handler(
                            {{GetRequiredService}}<global::IStore>(sp),
                            sp,
                            {{GetRequiredService}}<global::System.Collections.Generic.IEnumerable<global::IStore>>(sp))),
            """);
    }

    [Fact]
    public void NullableReferenceParameter_IsResolvedWithoutAnnotation()
    {
        var result = RunHost("""
            public sealed class Handler(IStore? store) : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """);

        Factory(result).Should().Contain($"{GetRequiredService}<global::IStore>(sp)");
        GeneratedWarnings(result).Should().BeEmpty();
    }

    [Theory]
    [InlineData("\"primary\"", "\"primary\"")]
    [InlineData("42", "42")]
    [InlineData("42L", "(long)42")]
    [InlineData("Region.America", "(global::Region)(-2)")]
    [InlineData("typeof(IStore)", "typeof(global::IStore)")]
    public void FromKeyedServices_WithConstantKey_ResolvesKeyedService(string key, string expectedLiteral)
    {
        var result = RunHost($$"""
            public sealed class Handler([FromKeyedServices({{key}})] IStore store) : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """);

        Factory(result).Should().Contain($"{GetRequiredKeyedService}<global::IStore>(sp, {expectedLiteral})");
        GeneratedWarnings(result).Should().BeEmpty();
    }

    [Theory]
    [InlineData("multiple public constructors", """
        public sealed class Handler : IRequestHandler<Cmd, string>
        {
            public Handler() { }
            public Handler(IStore store) { }
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("optional parameter", """
        public sealed class Handler(IStore store, int retries = 3) : IRequestHandler<Cmd, string>
        {
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("[ServiceKey] parameter", """
        public sealed class Handler([ServiceKey] string key) : IRequestHandler<Cmd, string>
        {
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("[FromKeyedServices] with inherited key", """
        public sealed class Handler([FromKeyedServices] IStore store) : IRequestHandler<Cmd, string>
        {
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("[FromKeyedServices] with null key", """
        public sealed class Handler([FromKeyedServices(null)] IStore store) : IRequestHandler<Cmd, string>
        {
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("params parameter", """
        public sealed class Handler(params IStore[] stores) : IRequestHandler<Cmd, string>
        {
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    [InlineData("required member", """
        public sealed class Handler : IRequestHandler<Cmd, string>
        {
            public required IStore Store { get; init; }
            public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
        }
        """)]
    public void UnsupportedConstructor_FallsBackToRegistrationByImplementationType(string reason, string handler)
    {
        var result = RunHost(handler);

        ShouldBeRegisteredByImplementationType(result);
        result.Registrations.Should().NotContain("new global::Handler(", reason);
        result.Diagnostics.Should().NotContain(d => d.Severity >= DiagnosticSeverity.Info, "the fallback is silent");
    }

    [Fact]
    public void ActivatorUtilitiesConstructor_SelectsThatConstructor()
    {
        var result = RunHost("""
            public sealed class Handler : IRequestHandler<Cmd, string>
            {
                public Handler() { }
                [ActivatorUtilitiesConstructor]
                public Handler(IStore store) { }
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """);

        Factory(result).Should().Contain($"static sp => new global::Handler(\n                {GetRequiredService}<global::IStore>(sp))),".ReplaceLineEndings());
    }

    [Fact]
    public void RequiredMembersSetByConstructor_AreConstructedDirectly()
    {
        var result = RunHost("""
            public sealed class Handler : IRequestHandler<Cmd, string>
            {
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public Handler(IStore store) => Store = store;
                public required IStore Store { get; init; }
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => default;
            }
            """);

        Factory(result).Should().Contain("static sp => new global::Handler(");
    }

    [Fact]
    public void LibraryHandlerWithoutDirectConstructor_HasNoFactoryInRegistry()
    {
        var registry = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public sealed record Cmd : IRequest;

            internal sealed class Handler(string name = "x") : IRequestHandler<Cmd, string>
            {
                public ValueTask<string> HandleAsync(Cmd request, CancellationToken cancellationToken = default) => new(name);
            }
            """, "HandlerLib").ShouldCompile().HandlerRegistry;

        registry.Should().Contain(
            "new(typeof(global::DecoratR.IRequestHandler<global::Cmd, string>), typeof(global::Handler)), // registered by implementation type");
    }

    [Fact]
    public void DecoratorDependencies_UseTheApplyMethodTypeParameters_EvenWhenSwapped()
    {
        var result = RunGenerator(TestSources.FullPath("""
            public interface IAudit<T>;

            [Decorator(Order = 1)]
            public sealed class AuditDecorator<TRes, TReq>(IAudit<TReq> audit, IRequestHandler<TReq, TRes> inner, IAudit<AuditDecorator<TRes, TReq>> self)
                : IRequestHandler<TReq, TRes>
                where TReq : IRequest
            {
                public ValueTask<TRes> HandleAsync(TReq request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Registrations.Should().Contain($$"""
                        (global::System.Func<global::System.IServiceProvider, global::DecoratR.IRequestHandler<TRequest, TResponse>>)(sp => new global::AuditDecorator<TResponse, TRequest>(
                            {{GetRequiredService}}<global::IAudit<TRequest>>(sp),
                            innerFactory(sp),
                            {{GetRequiredService}}<global::IAudit<global::AuditDecorator<TResponse, TRequest>>>(sp))),
            """);
        GeneratedWarnings(result).Should().BeEmpty();
    }

    [Fact]
    public void DecoratorWithTwoInnerHandlerParameters_FallsBackToWrap()
    {
        var result = RunGenerator(TestSources.FullPath("""
            [Decorator(Order = 1)]
            public sealed class TwiceDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> first, IRequestHandler<TRequest, TResponse> second)
                : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => first.HandleAsync(request, cancellationToken);
            }
            """)).ShouldCompile();

        result.Registrations.Should().Contain(
            "return Wrap<global::DecoratR.IRequestHandler<TRequest, TResponse>, global::TwiceDecorator<TRequest, TResponse>>(inner);");
        result.Registrations.Should().Contain("private static global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor Wrap<TService, [");
    }

    [Fact]
    public void AddDecoratR_ReturnsEarlyWhenAlreadyApplied()
    {
        var registrations = RunGenerator(TestSources.FullPath()).ShouldCompile().Registrations;

        registrations.Should().Contain("""
                    for (var i = 0; i < services.Count; i++)
                    {
                        if (services[i].ServiceType == typeof(Marker))
                        {
                            return services;
                        }
                    }

                    services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(Marker), Marker.Instance));
            """);
    }

    private static IEnumerable<Diagnostic> GeneratedWarnings(GenerationResult result) =>
        result.OutputCompilation.GetDiagnostics(Ct)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning &&
                        d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true);
}
