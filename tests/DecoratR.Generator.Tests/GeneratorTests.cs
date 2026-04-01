using System.Collections.Immutable;
using System.Reflection;
using DecoratR.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DecoratR.Generator.Tests;

public class GeneratorTests
{
    private static readonly string[] AbstractionsAssemblyPaths =
    [
        typeof(DecoratR.IRequestHandler<,>).Assembly.Location,
        typeof(object).Assembly.Location,
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll"),
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Threading.Tasks.dll")
    ];

    [Fact]
    public void NoAttribute_ProducesNoOutput()
    {
        var source = """
            using DecoratR;

            public sealed record TestCommand(string Name) : ICommand<string>;

            public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source, includeAttribute: false);

        // Only the attribute source should be generated, not the registrations
        Assert.Single(generatedTrees);
        Assert.Contains("GenerateHandlerRegistrationsAttribute", generatedTrees[0]);
    }

    [Fact]
    public void WithAttribute_CommandHandler_GeneratesRegistration()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestCommand(string Name) : ICommand<string>;

            public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        Assert.Equal(2, generatedTrees.Length); // attribute + registrations
        var registrations = generatedTrees.First(t => t.Contains("RegisterHandlers"));

        Assert.Contains("Command handler", registrations);
        Assert.Contains("TestCommandHandler", registrations);
        Assert.Contains("TestCommand", registrations);
        Assert.Contains("AddHandler", registrations);
    }

    [Fact]
    public void WithAttribute_QueryHandler_GeneratesRegistration()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestQuery(string Id) : IQuery<string>;

            public sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Result");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("RegisterHandlers"));
        Assert.Contains("Query handler", registrations);
        Assert.Contains("TestQueryHandler", registrations);
    }

    [Fact]
    public void WithAttribute_MultipleHandlers_GeneratesAll()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record Command1(string Name) : ICommand<string>;
            public sealed record Query1(string Id) : IQuery<int>;

            public sealed class Command1Handler : IRequestHandler<Command1, string>
            {
                public ValueTask<string> HandleAsync(Command1 request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }

            public sealed class Query1Handler : IRequestHandler<Query1, int>
            {
                public ValueTask<int> HandleAsync(Query1 request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult(42);
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("RegisterHandlers"));
        Assert.Contains("Command1Handler", registrations);
        Assert.Contains("Query1Handler", registrations);
    }

    [Fact]
    public void AbstractHandler_IsSkipped()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestCommand(string Name) : ICommand<string>;

            public abstract class AbstractHandler : IRequestHandler<TestCommand, string>
            {
                public abstract ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default);
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        // Should get a warning about no handlers found
        Assert.Contains(diagnostics, d => d.Id == "DCTR001");
    }

    [Fact]
    public void OpenGenericHandler_IsSkipped()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public class GenericHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => throw new System.NotImplementedException();
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR001");
    }

    [Fact]
    public void InternalHandler_IsIncluded()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestCommand(string Name) : ICommand<string>;

            internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("RegisterHandlers"));
        Assert.Contains("InternalHandler", registrations);
    }

    [Fact]
    public void WithAttribute_NoHandlers_ProducesWarning()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public class NotAHandler { }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR001");
        Assert.DoesNotContain(generatedTrees, t => t.Contains("RegisterHandlers"));
    }

    [Fact]
    public void WithHandlers_ProducesInfoDiagnostic()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestCommand(string Name) : ICommand<string>;

            public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR002");
    }

    [Fact]
    public void GeneratedClassName_DerivedFromAssemblyName()
    {
        var source = """
            using DecoratR;

            [assembly: DecoratR.GenerateHandlerRegistrations]

            public sealed record TestCommand(string Name) : ICommand<string>;

            public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
            {
                public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("Hello");
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source, assemblyName: "My.Test.Assembly");

        var registrations = generatedTrees.First(t => t.Contains("RegisterHandlers"));
        Assert.Contains("My_Test_AssemblyRegistrations", registrations);
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(
        string source,
        bool includeAttribute = true,
        string assemblyName = "TestAssembly")
    {
        var references = AbstractionsAssemblyPaths
            .Where(File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DecoratRIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }
}
