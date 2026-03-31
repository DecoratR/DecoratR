using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Decorate_wraps_existing_registration()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<TestCommand, string>, TestCommandHandler>();
        services.Decorate(
            typeof(IRequestHandler<TestCommand, string>),
            typeof(OuterDecorator<TestCommand, string>));

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        var result = await handler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);

        Assert.Equal("Outer(Handled: test)", result);
    }

    [Fact]
    public void Decorate_throws_when_service_not_registered()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.Decorate(
                typeof(IRequestHandler<TestCommand, string>),
                typeof(OuterDecorator<TestCommand, string>)));
    }

    [Fact]
    public async Task Decorate_preserves_lifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<TestCommand, string>, TestCommandHandler>();
        services.Decorate(
            typeof(IRequestHandler<TestCommand, string>),
            typeof(TrackingDecorator<TestCommand, string>));

        var provider = services.BuildServiceProvider();
        var handler1 = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();
        var handler2 = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        Assert.Same(handler1, handler2);
    }

    [Fact]
    public async Task Multiple_decorators_chain_correctly()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<TestCommand, string>, TestCommandHandler>();
        services.Decorate(
            typeof(IRequestHandler<TestCommand, string>),
            typeof(InnerDecorator<TestCommand, string>));
        services.Decorate(
            typeof(IRequestHandler<TestCommand, string>),
            typeof(OuterDecorator<TestCommand, string>));

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<TestCommand, string>>();

        var result = await handler.HandleAsync(new TestCommand("test"), TestContext.Current.CancellationToken);

        Assert.Equal("Outer(Inner(Handled: test))", result);
    }
}
