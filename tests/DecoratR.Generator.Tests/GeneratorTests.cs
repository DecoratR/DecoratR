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

        // 5 attribute files are emitted regardless, but no registrations
        Assert.Equal(5, generatedTrees.Length);
        Assert.Contains(generatedTrees, t => t.Contains("GenerateHandlerRegistrationsAttribute"));
        Assert.Contains(generatedTrees, t => t.Contains("GenerateDecoratRRegistrationsAttribute"));
        Assert.Contains(generatedTrees, t => t.Contains("DecoratRRegistrationMethodAttribute"));
        Assert.Contains(generatedTrees, t => t.Contains("DecoratRHandlerRegistrationAttribute"));
        Assert.Contains(generatedTrees, t => t.Contains("DecoratRHandlerServiceTypeAttribute"));
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

        Assert.Equal(6, generatedTrees.Length); // 5 attributes + registrations
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

    // ─── Handler-only path: Extension method generation ─────────────────────

    [Fact]
    public void HandlerOnly_GeneratesExtensionMethod()
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

        var registrations = generatedTrees.First(t => t.Contains("DecoratRHandlerServiceCollectionExtensions"));
        Assert.Contains("AddDecoratRHandlers_TestAssembly", registrations);
        Assert.Contains("ServiceDescriptor", registrations);
        Assert.Contains("TestCommandHandler", registrations);
    }

    [Fact]
    public void HandlerOnly_ExtensionMethodHasMarkerAttribute()
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

        var registrations = generatedTrees.First(t => t.Contains("DecoratRHandlerServiceCollectionExtensions"));
        Assert.Contains("[global::DecoratR.DecoratRRegistrationMethod]", registrations);
    }

    [Fact]
    public void HandlerOnly_GeneratesAssemblyLevelRegistrationAttribute()
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

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
        Assert.Contains("[assembly: global::DecoratR.DecoratRHandlerRegistration(", registrations);
        Assert.Contains("\"AddDecoratRHandlers_TestAssembly\"", registrations);
    }

    [Fact]
    public void HandlerOnly_GeneratesAssemblyLevelServiceTypeAttributes()
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

        var registrations = generatedTrees.First(t => t.Contains("HandlerRegistry"));
        Assert.Contains("[assembly: global::DecoratR.DecoratRHandlerServiceType(", registrations);
        Assert.Contains("\"global::TestCommand\"", registrations);
        Assert.Contains("\"string\"", registrations);
    }

    [Fact]
    public void HandlerOnly_InternalHandler_IncludedInExtensionMethod()
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

        var registrations = generatedTrees.First(t => t.Contains("DecoratRHandlerServiceCollectionExtensions"));
        Assert.Contains("InternalHandler", registrations);
        Assert.Contains("ServiceDescriptor", registrations);
    }

    [Fact]
    public void HandlerOnly_SanitizesAssemblyNameForMethodName()
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

        var registrations = generatedTrees.First(t => t.Contains("DecoratRHandlerServiceCollectionExtensions"));
        Assert.Contains("AddDecoratRHandlers_My_Test_Assembly", registrations);
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

                     [Decorator(Order = 1)]
                     public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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
        Assert.Contains("Decorate<", registrations);
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

                     [Decorator(Order = 1)]
                     public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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
        Assert.Contains("Decorate<", registrations);
        Assert.Contains("LoggingDecorator", registrations);
        // The handler IS registered via ServiceDescriptor, not the decorator
        Assert.Contains("TestCommandHandler", registrations);
        Assert.DoesNotContain("ServiceDescriptor", registrations.Split("// Apply decorators")[1]);
    }

    [Fact]
    public void FullAttribute_DecoratorAttributeClassification_ExcludedFromHandlerRegistry()
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

                     [Decorator(Order = 1)]
                     public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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

                     [Decorator(Order = 1)]
                     public class ADecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public ADecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }

                     [Decorator(Order = 2)]
                     public class BDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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

                     [Decorator(Order = 1)]
                     public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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

                     [Decorator(Order = 1)]
                     public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
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

    [Fact]
    public void FullAttribute_DecoratorOrder_RespectedInGeneration()
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

                     [Decorator(Order = 2)]
                     public class InnerDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public InnerDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }

                     [Decorator(Order = 1)]
                     public class OuterDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : IRequest
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public OuterDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // InnerDecorator (Order=2) should be applied before OuterDecorator (Order=1)
        // because we reverse: higher Order applied first = innermost
        var innerIdx = registrations.IndexOf("InnerDecorator", StringComparison.Ordinal);
        var outerIdx = registrations.IndexOf("OuterDecorator", StringComparison.Ordinal);

        // In the Decorate calls section, InnerDecorator should appear before OuterDecorator
        var decorateSection = registrations.Substring(registrations.IndexOf("// Apply decorators", StringComparison.Ordinal));
        var innerDecorateIdx = decorateSection.IndexOf("InnerDecorator", StringComparison.Ordinal);
        var outerDecorateIdx = decorateSection.IndexOf("OuterDecorator", StringComparison.Ordinal);
        Assert.True(innerDecorateIdx < outerDecorateIdx, "InnerDecorator (Order=2) should be applied first (closer to handler)");
    }

    // ─── Full path: Local handlers have "Register local handlers" comment ────

    [Fact]
    public void FullAttribute_LocalHandlers_MarkedAsLocal()
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
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));
        Assert.Contains("// Register local handlers", registrations);
        Assert.Contains("TestCommandHandler", registrations);
    }

    // ─── Cross-assembly two-stage tests ─────────────────────────────────────

    [Fact]
    public void TwoStage_CompositionRoot_CallsReferencedRegistrationMethod()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            handlerSource: """
                           using DecoratR;

                           [assembly: DecoratR.GenerateHandlerRegistrations]

                           public sealed record TestCommand(string Name) : IRequest;

                           public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                           {
                               public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                                   => ValueTask.FromResult("Hello");
                           }
                           """,
            compositionRootSource: """
                                   using DecoratR;

                                   [assembly: DecoratR.GenerateDecoratRRegistrations]
                                   """,
            handlerAssemblyName: "HandlerLib");

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));
        Assert.Contains("AddDecoratRHandlers_HandlerLib", registrations);
        Assert.Contains("// Register handlers from referenced assemblies", registrations);
    }

    [Fact]
    public void TwoStage_InternalHandler_DiscoveredViaAssemblyAttributes()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            handlerSource: """
                           using DecoratR;

                           [assembly: DecoratR.GenerateHandlerRegistrations]

                           public sealed record TestCommand(string Name) : IRequest;

                           internal sealed class InternalHandler : IRequestHandler<TestCommand, string>
                           {
                               public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                                   => ValueTask.FromResult("Hello");
                           }
                           """,
            compositionRootSource: """
                                   using DecoratR;

                                   [assembly: DecoratR.GenerateDecoratRRegistrations]
                                   """,
            handlerAssemblyName: "HandlerLib");

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // The composition root should call the registration method, not reference the handler directly
        Assert.Contains("AddDecoratRHandlers_HandlerLib", registrations);
        // Internal handler should NOT appear in composition root generated code
        Assert.DoesNotContain("InternalHandler", registrations);
    }

    [Fact]
    public void TwoStage_DecoratorsAppliedUsingReferencedServiceTypes()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            handlerSource: """
                           using DecoratR;

                           [assembly: DecoratR.GenerateHandlerRegistrations]

                           public sealed record TestCommand(string Name) : IRequest;

                           public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                           {
                               public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                                   => ValueTask.FromResult("Hello");
                           }
                           """,
            compositionRootSource: """
                                   using DecoratR;

                                   [assembly: DecoratR.GenerateDecoratRRegistrations]

                                   [Decorator(Order = 1)]
                                   public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                                       where TRequest : IRequest
                                   {
                                       private readonly IRequestHandler<TRequest, TResponse> _inner;
                                       public LoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                                       public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                                           => _inner.HandleAsync(request, cancellationToken);
                                   }
                                   """,
            handlerAssemblyName: "HandlerLib");

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // Decorators should be applied using service types from referenced assembly
        Assert.Contains("Decorate<", registrations);
        Assert.Contains("LoggingDecorator", registrations);
        Assert.Contains("TestCommand", registrations);
    }

    [Fact]
    public void TwoStage_MixedLocalAndReferencedHandlers()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            handlerSource: """
                           using DecoratR;

                           [assembly: DecoratR.GenerateHandlerRegistrations]

                           public sealed record RemoteCommand(string Name) : IRequest;

                           public sealed class RemoteHandler : IRequestHandler<RemoteCommand, string>
                           {
                               public ValueTask<string> HandleAsync(RemoteCommand request, CancellationToken cancellationToken = default)
                                   => ValueTask.FromResult("Remote");
                           }
                           """,
            compositionRootSource: """
                                   using DecoratR;

                                   [assembly: DecoratR.GenerateDecoratRRegistrations]

                                   public sealed record LocalCommand(string Name) : IRequest;

                                   public sealed class LocalHandler : IRequestHandler<LocalCommand, string>
                                   {
                                       public ValueTask<string> HandleAsync(LocalCommand request, CancellationToken cancellationToken = default)
                                           => ValueTask.FromResult("Local");
                                   }
                                   """,
            handlerAssemblyName: "HandlerLib");

        var registrations = generatedTrees.First(t => t.Contains("DecoratRServiceCollectionExtensions"));

        // Referenced handlers via method call
        Assert.Contains("AddDecoratRHandlers_HandlerLib", registrations);
        // Local handlers via direct AddTransient
        Assert.Contains("LocalHandler", registrations);
        Assert.Contains("// Register local handlers", registrations);
        Assert.Contains("// Register handlers from referenced assemblies", registrations);
    }

    // ─── Helper methods ─────────────────────────────────────────────────────

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

    /// <summary>
    /// Runs a two-stage generator test:
    /// 1. Compiles the handler project with [GenerateHandlerRegistrations], runs the generator
    /// 2. Compiles the composition root referencing the handler assembly, runs the generator
    /// Returns the composition root's generated output.
    /// </summary>
    private static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunTwoStageGenerator(
        string handlerSource,
        string compositionRootSource,
        string handlerAssemblyName = "HandlerLib",
        string compositionRootAssemblyName = "CompositionRoot")
    {
        var references = AbstractionsAssemblyPaths
            .Where(File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        // Stage 1: Compile handler project with generator
        var handlerCompilation = CSharpCompilation.Create(
            handlerAssemblyName,
            [CSharpSyntaxTree.ParseText(handlerSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator1 = new DecoratRIncrementalGenerator();
        GeneratorDriver handlerDriver = CSharpGeneratorDriver.Create(generator1);

        handlerDriver = handlerDriver.RunGeneratorsAndUpdateCompilation(
            handlerCompilation, out var handlerOutputCompilation, out _);

        // Create a metadata reference from the handler compilation output
        var handlerRef = handlerOutputCompilation.ToMetadataReference();

        // Stage 2: Compile composition root referencing the handler assembly
        var compositionRootReferences = references.ToList();
        compositionRootReferences.Add(handlerRef);

        var compositionRootCompilation = CSharpCompilation.Create(
            compositionRootAssemblyName,
            [CSharpSyntaxTree.ParseText(compositionRootSource)],
            compositionRootReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator2 = new DecoratRIncrementalGenerator();
        GeneratorDriver compositionRootDriver = CSharpGeneratorDriver.Create(generator2);

        compositionRootDriver = compositionRootDriver.RunGeneratorsAndUpdateCompilation(
            compositionRootCompilation, out _, out var diagnostics);

        var runResult = compositionRootDriver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }
}
