using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class NoTriggerTests : GeneratorTestBase
{
    [Fact]
    public void WithoutAssemblyAttribute_EmitsNothing()
    {
        var result = RunGenerator($"""
            using DecoratR;

            {TestSources.TestCommandRecord}

            {TestSources.TestCommandHandler}
            """).ShouldCompile();

        result.Sources.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void WithoutAssemblyAttribute_DoesNotReportDetectionDiagnostics()
    {
        var result = RunGenerator("""
            using DecoratR;

            [Decorator]
            public class NotGeneric { }
            """).ShouldCompile();

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void TriggerAttributes_AreDefinedInAbstractions()
    {
        typeof(GenerateDecoratRMetadataAttribute).Assembly.GetName().Name.Should().Be("DecoratR.Abstractions");
        typeof(GenerateDecoratRRegistrationsAttribute).Assembly.GetName().Name.Should().Be("DecoratR.Abstractions");
        typeof(Metadata.DecoratRHandlerAttribute).Assembly.GetName().Name.Should().Be("DecoratR.Abstractions");
    }
}
