using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DecoratR.Generator.Tests;

public class GeneratorTests
{
    private static readonly string[] AbstractionsAssemblyPaths =
    [
        typeof(IRequestHandler<,>).Assembly.Location,
        typeof(object).Assembly.Location,
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll"),
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Threading.Tasks.dll")
    ];

    // ─── Handler-only path ([GenerateHandlerRegistrations]) ─────────────────

    [Fact]
    public void NoAttribute_ProducesOnlyAttributeSources()
    {
        var source = """
                     using DecoratR;

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        // Both attribute files are emitted regardless, but no registrations
        Assert.Equal(2, generatedTrees.Length);
        Assert.Contains(generatedTrees, t => t.Contains("GenerateHandlerRegistrationsAttribute"));
        Assert.Contains(generatedTrees, t => t.Contains("GenerateDecoratRRegistrationsAttribute"));
        Assert.DoesNotContain(generatedTrees, t => t.Contains("HandlerRegistry"));
    }

    [Fact]
    public void WithAttribute_CommandHandler_GeneratesRegistration()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        Assert.Equal(3, generatedTrees.Length); // 2 attributes + registrations
        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));

        Assert.Contains("TestCommandHandler", registrations);
        Assert.Contains("TestCommand", registrations);
        Assert.Contains("IRequestHandler", registrations);
    }

    [Fact]
    public void WithAttribute_QueryHandler_GeneratesRegistration()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestQuery(string Id) : IRequest;

                     public sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
                     {
                         public ValueTask<string> HandleAsync(TestQuery request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Result");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
        Assert.Contains("TestQueryHandler", registrations);
    }

    [Fact]
    public void WithAttribute_MultipleHandlers_GeneratesAll()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record Command1(string Name) : IRequest;
                     public sealed record Query1(string Id) : IRequest;

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

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
        Assert.Contains("Command1Handler", registrations);
        Assert.Contains("Query1Handler", registrations);
    }

    [Fact]
    public void AbstractHandler_IsSkipped()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public abstract class AbstractHandler : IRequestHandler<TestCommand, string>
                     {
                         public abstract ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default);
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR001");
    }

    [Fact]
    public void OpenGenericHandler_IsSkipped()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public class GenericHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => throw new System.NotImplementedException();
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR001");
    }

    [Fact]
    public void InternalHandler_IsIncluded()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
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
        Assert.DoesNotContain(generatedTrees, t => t.Contains("HandlerRegistry"));
    }

    [Fact]
    public void WithHandlers_ProducesInfoDiagnostic()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR002");
    }

    [Fact]
    public void GeneratedNamespace_DerivedFromAssemblyName()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateHandlerRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source, assemblyName: "My.Test.Assembly");

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
        Assert.Contains("namespace My.Test.Assembly", registrations);
    }

    // ─── Full path ([GenerateDecoratRRegistrations]) ─────────────────────────

    [Fact]
    public void FullAttribute_WithHandlerAndDecorator_GeneratesAddDecoratR()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));
        Assert.Contains("TestCommandHandler", registrations);
        Assert.Contains("LoggingDecorator", registrations);
        Assert.Contains("Decorate(services,", registrations);
    }

    [Fact]
    public void FullAttribute_DecoratorExcludedFromHandlerList()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // The decorator IS referenced in Decorate() calls
        Assert.Contains("Decorate(services,", registrations);
        Assert.Contains("LoggingDecorator", registrations);
        // The handler IS registered via AddTransient, not the decorator
        Assert.Contains("TestCommandHandler", registrations);
        Assert.DoesNotContain("AddTransient", registrations.Split("// Apply decorators")[1]);
    }

    [Fact]
    public void FullAttribute_IDecoratorClassification_ExcludedFromHandlerRegistry()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // HandlerRegistry.Handlers should contain TestCommandHandler but not LoggingDecorator
        var handlerRegistrySection = registrations.Split("public static class HandlerRegistry")[1].Split("public static class DecoratorRegistry")[0];
        Assert.Contains("TestCommandHandler", handlerRegistrySection);
        Assert.DoesNotContain("LoggingDecorator", handlerRegistrySection);
    }

    [Fact]
    public void FullAttribute_MultipleDecorators_AllAppliedToHandler()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class ADecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public ADecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }

                     public class BDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public BDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));
        Assert.Contains("ADecorator", registrations);
        Assert.Contains("BDecorator", registrations);
    }

    [Fact]
    public void FullAttribute_DecoratorsDiscovered_ProducesInfoDiagnostic()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (diagnostics, _) = RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "DCTR003");
    }

    [Fact]
    public void FullAttribute_GeneratesDecoratorRegistry()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public sealed record TestCommand(string Name) : IRequest;

                     public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                     {
                         public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("Hello");
                     }

                     public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratorRegistry"));
        Assert.Contains("DecoratorRegistry", registrations);
        Assert.Contains("LoggingDecorator", registrations);
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(
        string source,
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
            compilation, out _, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }
}
