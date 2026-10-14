using AwesomeAssertions;
using DecoratR.IntegrationTests.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DecoratR.IntegrationTests;

/// <summary>
/// Runtime regression tests for the findings of <c>docs/modular-monolith-analysis.md</c> (probes R2, R3). They assert
/// the target behavior; the phase that implements a finding removes the skip.
/// </summary>
public class ModularMonolithRegressionTests
{
    [Fact(Skip = "MM1 · Phase 1.4: dekorierte Handler sind Factory-Registrierungen, ValidateOnBuild prüft ihre Abhängigkeiten nicht")]
    public void MM1_ValidateOnBuild_ReportsMissingDependencyOfDecoratedHandler()
    {
        // ExecutionTrace, a dependency of every handler and decorator, is deliberately not registered: in a modular
        // monolith this is the module whose AddXModule() call was forgotten.
        var services = new ServiceCollection();
        services.AddDecoratR();

        var build = () => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        build.Should().Throw<AggregateException>().WithMessage("*ExecutionTrace*");
    }

    [Fact(Skip = "MM7 · Phase 1.5: AddDecoratR() ist nicht idempotent, ein zweiter Aufruf dekoriert die erste Registrierung erneut")]
    public async Task MM7_AddDecoratRTwice_RegistersAndDecoratesOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionTrace>();
        services.AddDecoratR();
        services.AddDecoratR();
        using var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IRequestHandler<HostCommand, string>>().ToList();
        handlers.Should().ContainSingle();

        await handlers[0].HandleAsync(new HostCommand("Ada"), TestContext.Current.CancellationToken);
        provider.GetRequiredService<ExecutionTrace>().Entries.Count(e => e == "HostOuterDecorator:enter").Should().Be(1);
    }
}
