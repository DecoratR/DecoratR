namespace DecoratR.Generator.Tests.Fixtures;

public static class TestSources
{
    // ── Assembly Attributes ─────────────────────────────────────────────

    public const string MetadataAttribute = "[assembly: DecoratR.GenerateDecoratRMetadata]";

    public const string RegistrationsAttribute = "[assembly: DecoratR.GenerateDecoratRRegistrations]";

    // ── Request Types ───────────────────────────────────────────────────

    public const string TestCommandRecord = "public sealed record TestCommand(string Name) : IRequest;";

    public const string TestQueryRecord = "public sealed record TestQuery(string Id) : IRequest;";

    // ── Handlers ────────────────────────────────────────────────────────

    public const string TestCommandHandler = """
                                             public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                                             {
                                                 public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                                                     => ValueTask.FromResult("Hello");
                                             }
                                             """;

    public const string TestQueryHandler = """
                                           public sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
                                           {
                                               public ValueTask<string> HandleAsync(TestQuery request, CancellationToken cancellationToken = default)
                                                   => ValueTask.FromResult("Result");
                                           }
                                           """;

    // ── Constraint scenario sources ─────────────────────────────────────

    public const string CommandQueryInterfaces = """
                                                 public interface ICommand : IRequest;
                                                 public interface IQuery : IRequest;
                                                 """;

    public const string CommandQueryHandlers = """
                                               public sealed record CreateUserCommand(string Name) : ICommand;
                                               public sealed record GetUsersQuery : IQuery;

                                               public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, string>
                                               {
                                                   public ValueTask<string> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken = default)
                                                       => ValueTask.FromResult("created");
                                               }

                                               public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, string>
                                               {
                                                   public ValueTask<string> HandleAsync(GetUsersQuery request, CancellationToken cancellationToken = default)
                                                       => ValueTask.FromResult("users");
                                               }
                                               """;

    // ── Decorators ──────────────────────────────────────────────────────

    public static string Decorator(string name, int order, string constraint = "IRequest")
    {
        return $$"""
                 [Decorator(Order = {{order}})]
                 public class {{name}}<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                     where TRequest : {{constraint}}
                 {
                     private readonly IRequestHandler<TRequest, TResponse> _inner;
                     public {{name}}(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                     public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                         => _inner.HandleAsync(request, cancellationToken);
                 }
                 """;
    }

    public static string InternalDecorator(string name, int order, string constraint = "IRequest")
    {
        return $$"""
                 [Decorator(Order = {{order}})]
                 internal class {{name}}<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                     where TRequest : {{constraint}}
                 {
                     private readonly IRequestHandler<TRequest, TResponse> _inner;
                     public {{name}}(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                     public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                         => _inner.HandleAsync(request, cancellationToken);
                 }
                 """;
    }

    // ── Composite source builders ───────────────────────────────────────

    public static string HandlerOnly(string body = "")
    {
        return $"""
                using DecoratR;

                {MetadataAttribute}

                {TestCommandRecord}

                {TestCommandHandler}
                {body}
                """;
    }

    public static string FullPath(string body = "")
    {
        return $"""
                using DecoratR;

                {RegistrationsAttribute}

                {TestCommandRecord}

                {TestCommandHandler}
                {body}
                """;
    }

    // ── Stream Request Types ────────────────────────────────────────────

    public const string TestStreamQueryRecord = "public sealed record TestStreamQuery(string Filter) : IStreamRequest;";

    // ── Stream Handlers ─────────────────────────────────────────────────

    public const string TestStreamQueryHandler = """
                                                 public sealed class TestStreamQueryHandler : IStreamRequestHandler<TestStreamQuery, string>
                                                 {
                                                     public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
                                                     {
                                                         yield return "item1";
                                                         yield return "item2";
                                                     }
                                                 }
                                                 """;

    // ── Stream Decorators ───────────────────────────────────────────────

    public static string StreamDecorator(string name, int order, string constraint = "IStreamRequest")
    {
        return $$"""
                 [Decorator(Order = {{order}})]
                 public class {{name}}<TRequest, TResponse> : IStreamRequestHandler<TRequest, TResponse>
                     where TRequest : {{constraint}}
                 {
                     private readonly IStreamRequestHandler<TRequest, TResponse> _inner;
                     public {{name}}(IStreamRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                     public IAsyncEnumerable<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                         => _inner.HandleAsync(request, cancellationToken);
                 }
                 """;
    }

    // ── Stream composite source builders ────────────────────────────────

    public static string StreamHandlerOnly(string body = "")
    {
        return $"""
                using DecoratR;

                {MetadataAttribute}

                {TestStreamQueryRecord}

                {TestStreamQueryHandler}
                {body}
                """;
    }

    public static string StreamFullPath(string body = "")
    {
        return $"""
                using DecoratR;

                {RegistrationsAttribute}

                {TestStreamQueryRecord}

                {TestStreamQueryHandler}
                {body}
                """;
    }

    public static string MixedFullPath(string body = "")
    {
        return $"""
                using DecoratR;

                {RegistrationsAttribute}

                {TestCommandRecord}
                {TestCommandHandler}

                {TestStreamQueryRecord}
                {TestStreamQueryHandler}
                {body}
                """;
    }
}