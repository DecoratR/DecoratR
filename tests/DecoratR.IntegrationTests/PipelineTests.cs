using AwesomeAssertions;
using DecoratR.IntegrationTests.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DecoratR.IntegrationTests;

/// <summary>
/// Runs the real generated <c>AddDecoratR()</c> against a real <see cref="ServiceProvider"/> and observes the
/// execution order of decorators and handlers across the host and the referenced library.
/// </summary>
public class PipelineTests
{
    private static (ServiceProvider Provider, ExecutionTrace Trace) Build(Action<DecoratROptions>? configure = null, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionTrace>();
        extra?.Invoke(services);
        services.AddDecoratR(configure);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return (provider, provider.GetRequiredService<ExecutionTrace>());
    }

    [Fact]
    public async Task HostCommand_RunsHostAndLibraryDecoratorsInGlobalOrder()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<HostCommand, string>>();
        var result = await handler.HandleAsync(new HostCommand("Ada"), TestContext.Current.CancellationToken);

        result.Should().Be("Host: Ada");
        trace.Entries.Should().Equal(
            "HostOuterDecorator:enter",          // Order 0
            "HostTiedDecorator:enter",           // Order 5, wins the tie by type name
            "LibraryLoggingDecorator:enter",     // Order 5
            "LibraryCommandDecorator:enter",     // Order 10, constrained to ILibraryCommand
            "HostCommandHandler",
            "LibraryCommandDecorator:exit",
            "LibraryLoggingDecorator:exit",
            "HostTiedDecorator:exit",
            "HostOuterDecorator:exit");
    }

    [Fact]
    public async Task LibraryCommand_InternalHandlerAndDecorators_AreComposedThroughRegistry()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<LibraryCommand, string>>();
        var result = await handler.HandleAsync(new LibraryCommand("Grace"), TestContext.Current.CancellationToken);

        result.Should().Be("Hello, Grace");
        trace.Entries.Should().Equal(
            "HostOuterDecorator:enter",
            "HostTiedDecorator:enter",
            "LibraryLoggingDecorator:enter",
            "LibraryCommandDecorator:enter",
            "LibraryCommandHandler",
            "LibraryCommandDecorator:exit",
            "LibraryLoggingDecorator:exit",
            "HostTiedDecorator:exit",
            "HostOuterDecorator:exit");
    }

    [Fact]
    public async Task Query_OnlyMatchingConstraintsApply()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<HostQuery, int>>();
        var result = await handler.HandleAsync(new HostQuery(), TestContext.Current.CancellationToken);

        result.Should().Be(7);
        trace.Entries.Should().Equal(
            "HostOuterDecorator:enter",
            "HostTiedDecorator:enter",
            "LibraryLoggingDecorator:enter",
            "HostStructResponseDecorator:enter", // TResponse : struct matches int
            "HostQueryHandler",                  // LibraryCommandDecorator does not apply (not an ILibraryCommand)
            "HostStructResponseDecorator:exit",
            "LibraryLoggingDecorator:exit",
            "HostTiedDecorator:exit",
            "HostOuterDecorator:exit");
    }

    [Fact]
    public async Task StringResponse_DoesNotGetStructResponseDecorator()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        await provider.GetRequiredService<IRequestHandler<HostCommand, string>>()
            .HandleAsync(new HostCommand("x"), TestContext.Current.CancellationToken);

        trace.Entries.Should().NotContain(e => e.StartsWith("HostStructResponseDecorator"));
    }

    [Fact]
    public async Task HostVoidCommand_ResolvesAsPlainValueTask_AndRunsAllMatchingDecorators()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<HostVoidCommand>>();
        await handler.HandleAsync(new HostVoidCommand("Linus"), TestContext.Current.CancellationToken);

        handler.Should().BeOfType<VoidRequestHandler<HostVoidCommand>>();
        trace.Entries.Should().Equal(
            "HostOuterDecorator:enter",
            "HostTiedDecorator:enter",
            "LibraryLoggingDecorator:enter",
            "HostStructResponseDecorator:enter", // Unit is a struct
            "LibraryCommandDecorator:enter",     // HostVoidCommand is an ILibraryCommand
            "HostVoidCommandHandler:Linus",
            "LibraryCommandDecorator:exit",
            "HostStructResponseDecorator:exit",
            "LibraryLoggingDecorator:exit",
            "HostTiedDecorator:exit",
            "HostOuterDecorator:exit");
    }

    [Fact]
    public async Task LibraryVoidCommand_InternalHandler_IsExposedThroughRegistry()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<LibraryVoidCommand>>();
        await handler.HandleAsync(new LibraryVoidCommand(), TestContext.Current.CancellationToken);

        trace.Entries.Should().Equal(
            "HostOuterDecorator:enter",
            "HostTiedDecorator:enter",
            "LibraryLoggingDecorator:enter",
            "HostStructResponseDecorator:enter",
            "LibraryVoidCommandHandler",
            "HostStructResponseDecorator:exit",
            "LibraryLoggingDecorator:exit",
            "HostTiedDecorator:exit",
            "HostOuterDecorator:exit");
    }

    [Fact]
    public async Task VoidCommand_UnitPipeline_IsResolvableToo()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IRequestHandler<HostVoidCommand, Unit>>();
        var result = await handler.HandleAsync(new HostVoidCommand("Ada"), TestContext.Current.CancellationToken);

        result.Should().Be(Unit.Value);
        handler.Should().BeOfType<HostOuterDecorator<HostVoidCommand, Unit>>();
        trace.Entries.Should().Contain("HostVoidCommandHandler:Ada");
    }

    [Fact]
    public void VoidFacade_SharesTheLifetimeOfThePipeline()
    {
        var (provider, _) = Build(options => options.Lifetime = ServiceLifetime.Scoped);
        using var _ = provider;

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IRequestHandler<HostVoidCommand>>();
        var b = scope1.ServiceProvider.GetRequiredService<IRequestHandler<HostVoidCommand>>();
        var c = scope2.ServiceProvider.GetRequiredService<IRequestHandler<HostVoidCommand>>();

        a.Should().BeSameAs(b);
        a.Should().NotBeSameAs(c);
    }

    [Fact]
    public async Task StreamPipeline_IsSeparateFromRequestPipeline()
    {
        var (provider, trace) = Build();
        using var _ = provider;

        var handler = provider.GetRequiredService<IStreamRequestHandler<LibraryStream, int>>();
        var items = new List<int>();
        await foreach (var item in handler.HandleAsync(new LibraryStream(3), TestContext.Current.CancellationToken))
            items.Add(item);

        items.Should().Equal(0, 1, 2);
        trace.Entries.Should().Equal(
            "LibraryStreamDecorator:enter",
            "LibraryStreamHandler",
            "LibraryStreamDecorator:exit");
    }

    [Fact]
    public void DefaultLifetime_IsTransient()
    {
        var (provider, _) = Build();
        using var _ = provider;

        var first = provider.GetRequiredService<IRequestHandler<HostQuery, int>>();
        var second = provider.GetRequiredService<IRequestHandler<HostQuery, int>>();

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void ScopedLifetime_IsAppliedToWholePipeline()
    {
        var (provider, _) = Build(options => options.Lifetime = ServiceLifetime.Scoped);
        using var _ = provider;

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IRequestHandler<HostQuery, int>>();
        var b = scope1.ServiceProvider.GetRequiredService<IRequestHandler<HostQuery, int>>();
        var c = scope2.ServiceProvider.GetRequiredService<IRequestHandler<HostQuery, int>>();

        a.Should().BeSameAs(b);
        a.Should().NotBeSameAs(c);
        a.Should().BeOfType<HostOuterDecorator<HostQuery, int>>();
    }

    [Fact]
    public async Task ManualFactoryAndInstanceRegistrations_AreDecoratedToo()
    {
        var manualTrace = new ExecutionTrace();
        var (provider, trace) = Build(extra: services =>
        {
            services.AddTransient<IRequestHandler<HostQuery, int>>(_ => new HostQueryHandler(manualTrace));
            services.AddSingleton<IRequestHandler<HostQuery, int>>(new HostQueryHandler(manualTrace));
        });
        using var _ = provider;

        var handlers = provider.GetServices<IRequestHandler<HostQuery, int>>().ToList();
        handlers.Should().HaveCount(3, "the two manual registrations and the generated one are all decorated");
        handlers.Should().AllBeOfType<HostOuterDecorator<HostQuery, int>>();

        foreach (var handler in handlers)
            await handler.HandleAsync(new HostQuery(), TestContext.Current.CancellationToken);

        manualTrace.Entries.Should().Equal("HostQueryHandler", "HostQueryHandler");
        trace.Entries.Count(e => e == "HostQueryHandler").Should().Be(1);
        trace.Entries.Count(e => e == "HostOuterDecorator:enter").Should().Be(3);
    }

    [Fact]
    public async Task KeyedRegistration_IsLeftUndecorated()
    {
        var (provider, trace) = Build(extra: services =>
            services.AddKeyedTransient<IRequestHandler<HostQuery, int>, HostQueryHandler>("raw"));
        using var _ = provider;

        var raw = provider.GetRequiredKeyedService<IRequestHandler<HostQuery, int>>("raw");
        raw.Should().BeOfType<HostQueryHandler>();

        await raw.HandleAsync(new HostQuery(), TestContext.Current.CancellationToken);
        trace.Entries.Should().Equal("HostQueryHandler");
    }

    [Fact]
    public void GeneratedExtension_LivesInDependencyInjectionNamespace()
    {
        var type = typeof(DecoratRServiceCollectionExtensions);

        type.Namespace.Should().Be("Microsoft.Extensions.DependencyInjection");
        type.GetMethod("AddDecoratR").Should().NotBeNull();
    }
}
