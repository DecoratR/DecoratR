using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MsDi = Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddDecoratR_discovers_handlers_from_assembly()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options =>
        {
            options.RegisterHandlersFromAssembly(Assembly.GetExecutingAssembly());
        });

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetService<IRequestHandler<TestQuery, string>>();

        Assert.NotNull(commandHandler);
        Assert.NotNull(queryHandler);
    }

    [Fact]
    public void RegisterHandlersFromAssemblyContaining_discovers_handlers()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options =>
            options.RegisterHandlersFromAssembly<TestCommand>());

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetService<IRequestHandler<TestQuery, string>>();

        Assert.NotNull(commandHandler);
        Assert.NotNull(queryHandler);
    }

    [Fact]
    public async Task Handler_returns_correct_result_without_decorators()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options =>
            options.RegisterHandlersFromAssembly<TestCommand>());

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        var result = await handler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);

        Assert.Equal("Handled: test", result);
    }

    [Fact]
    public async Task Decorators_are_applied_in_correct_order()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddDecorator(typeof(OuterDecorator<,>))
            .AddDecorator(typeof(InnerDecorator<,>)));

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        var result = await handler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);

        // Outer wraps Inner wraps Handler
        Assert.Equal("Outer(Inner(Handled: test))", result);
    }

    [Fact]
    public async Task Decorator_is_invoked_on_each_call()
    {
        TrackingDecorator<TestCommand, string>.Reset();

        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddDecorator(typeof(TrackingDecorator<,>)));

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        await handler.HandleAsync(new TestCommand("a"), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new TestCommand("b"), TestContext.Current.CancellationToken);

        Assert.Equal(2, TrackingDecorator<TestCommand, string>.CallCount);
    }

    [Fact]
    public void AddDecorator_throws_for_non_open_generic()
    {
        var options = new DecoratROptions();

        Assert.Throws<ArgumentException>(() =>
            options.AddDecorator(typeof(string)));
    }

    [Fact]
    public void AddDecorator_throws_for_open_generic_that_does_not_implement_handler()
    {
        var options = new DecoratROptions();

        Assert.Throws<ArgumentException>(() =>
            options.AddDecorator(typeof(List<>)));
    }

    [Fact]
    public void Handlers_are_registered_as_transient()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options =>
            options.RegisterHandlersFromAssembly<TestCommand>());

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var handler1 = scope1.ServiceProvider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var handler2 = scope2.ServiceProvider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public async Task AddCommandDecorator_applies_only_to_command_handlers()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddCommandDecorator(typeof(OuterDecorator<,>)));

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetRequiredService<IRequestHandler<TestQuery, string>>();

        var commandResult = await commandHandler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new TestQuery(1), TestContext.Current.CancellationToken);

        Assert.Equal("Outer(Handled: test)", commandResult);
        Assert.Equal("Result: 1", queryResult); // no decorator applied
    }

    [Fact]
    public async Task AddQueryDecorator_applies_only_to_query_handlers()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddQueryDecorator(typeof(OuterDecorator<,>)));

        await using var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetRequiredService<IRequestHandler<TestQuery, string>>();

        var commandResult = await commandHandler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new TestQuery(1), TestContext.Current.CancellationToken);

        Assert.Equal("Handled: test", commandResult); // no decorator applied
        Assert.Equal("Outer(Result: 1)", queryResult);
    }

    [Fact]
    public async Task AddDecorator_with_predicate_filters_handlers()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddDecorator(typeof(OuterDecorator<,>), requestType => requestType == typeof(TestCommand)));

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetRequiredService<IRequestHandler<TestQuery, string>>();

        var commandResult = await commandHandler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new TestQuery(1), TestContext.Current.CancellationToken);

        Assert.Equal("Outer(Handled: test)", commandResult);
        Assert.Equal("Result: 1", queryResult); // no decorator applied
    }

    [Fact]
    public void WithLifetime_defaults_to_transient()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options =>
            options.RegisterHandlersFromAssembly<TestCommand>());

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IRequestHandler<TestCommand, string>));

        Assert.Equal(MsDi.ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void Handlers_are_registered_with_configured_lifetime()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .WithLifetime(ServiceLifetime.Scoped));

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IRequestHandler<TestCommand, string>));

        Assert.Equal(MsDi.ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Scoped_handlers_return_same_instance_within_scope()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .WithLifetime(ServiceLifetime.Scoped));

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var handler1 = scope.ServiceProvider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var handler2 = scope.ServiceProvider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        Assert.Same(handler1, handler2);
    }

    [Fact]
    public async Task Fluent_chaining_configures_pipeline_correctly()
    {
        var services = new ServiceCollection();
        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly<TestCommand>()
            .AddDecorator(typeof(OuterDecorator<,>))
            .AddCommandDecorator(typeof(InnerDecorator<,>)));

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetRequiredService<IRequestHandler<TestQuery, string>>();

        var commandResult = await commandHandler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new TestQuery(1), TestContext.Current.CancellationToken);

        // Command: both decorators applied
        Assert.Equal("Outer(Inner(Handled: test))", commandResult);
        // Query: only Outer applied (Inner is command-only)
        Assert.Equal("Outer(Result: 1)", queryResult);
    }

    [Fact]
    public void Duplicate_assembly_registration_registers_handlers_only_once()
    {
        var services = new ServiceCollection();
        var assembly = Assembly.GetExecutingAssembly();

        services.AddDecoratR(options => options
            .RegisterHandlersFromAssembly(assembly)
            .RegisterHandlersFromAssembly(assembly));

        var handlerDescriptors = services
            .Where(s => s.ServiceType == typeof(IRequestHandler<TestCommand, string>))
            .ToList();

        Assert.Single(handlerDescriptors);
    }

    [Fact]
    public void AddHandler_registers_handler_without_reflection()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options => options
            .AddHandler<TestCommand, string, TestCommandHandler>());

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestCommand, string>>();

        Assert.NotNull(handler);
        Assert.IsType<TestCommandHandler>(handler);
    }

    [Fact]
    public async Task AddHandler_handler_returns_correct_result()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options => options
            .AddHandler<TestCommand, string, TestCommandHandler>());

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        var result = await handler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);

        Assert.Equal("Handled: test", result);
    }

    [Fact]
    public async Task AddHandler_with_decorators_applies_correctly()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options => options
            .AddHandler<TestCommand, string, TestCommandHandler>()
            .AddHandler<TestQuery, string, TestQueryHandler>()
            .AddDecorator(typeof(OuterDecorator<,>))
            .AddCommandDecorator(typeof(InnerDecorator<,>)));

        var provider = services.BuildServiceProvider();

        var commandHandler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var queryHandler = provider.GetRequiredService<IRequestHandler<TestQuery, string>>();

        var commandResult = await commandHandler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new TestQuery(1), TestContext.Current.CancellationToken);

        Assert.Equal("Outer(Inner(Handled: test))", commandResult);
        Assert.Equal("Outer(Result: 1)", queryResult);
    }

    [Fact]
    public void AddHandler_respects_configured_lifetime()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options => options
            .AddHandler<TestCommand, string, TestCommandHandler>()
            .WithLifetime(ServiceLifetime.Scoped));

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IRequestHandler<TestCommand, string>));

        Assert.Equal(MsDi.ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddHandler_does_not_trigger_assembly_fallback()
    {
        var services = new ServiceCollection();

        services.AddDecoratR(options => options
            .AddHandler<TestCommand, string, TestCommandHandler>());

        // Only the explicitly added handler should be registered,
        // not handlers discovered via assembly scanning fallback
        var handlerDescriptors = services
            .Where(s => s.ServiceType.IsGenericType && s.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToList();

        Assert.Single(handlerDescriptors);
    }
}