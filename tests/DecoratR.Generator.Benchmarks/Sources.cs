namespace DecoratR.Generator.Benchmarks;

/// <summary>
/// Source code snippets for benchmark scenarios.
/// Mirrors the patterns from TestSources but standalone.
/// </summary>
internal static class Sources
{
    private const string Usings = "using DecoratR;";

    // ── Scenario 1: Single handler + single decorator ───────────────────

    public const string SingleHandlerWithDecorator = $$"""
        {{Usings}}

        [assembly: DecoratR.GenerateDecoratRMetadata]

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

    // ── Scenario 2: Multiple handlers + multiple decorators ─────────────

    public const string MultipleHandlersWithDecorators = $$"""
        {{Usings}}

        [assembly: DecoratR.GenerateDecoratRMetadata]

        public sealed record Command1(string Name) : IRequest;
        public sealed record Command2(string Name) : IRequest;
        public sealed record Command3(string Name) : IRequest;
        public sealed record Query1(string Id) : IRequest;
        public sealed record Query2(string Id) : IRequest;

        public sealed class Handler1 : IRequestHandler<Command1, string>
        {
            public ValueTask<string> HandleAsync(Command1 request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("1");
        }

        public sealed class Handler2 : IRequestHandler<Command2, string>
        {
            public ValueTask<string> HandleAsync(Command2 request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("2");
        }

        public sealed class Handler3 : IRequestHandler<Command3, string>
        {
            public ValueTask<string> HandleAsync(Command3 request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("3");
        }

        public sealed class QueryHandler1 : IRequestHandler<Query1, string>
        {
            public ValueTask<string> HandleAsync(Query1 request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("q1");
        }

        public sealed class QueryHandler2 : IRequestHandler<Query2, string>
        {
            public ValueTask<string> HandleAsync(Query2 request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("q2");
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

        [Decorator(Order = 2)]
        public class ValidationDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public ValidationDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 3)]
        public class RetryDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public RetryDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }
        """;

    // ── Scenario 3: Constrained decorators with command/query separation ─

    public const string ConstrainedDecorators = $$"""
        {{Usings}}

        [assembly: DecoratR.GenerateDecoratRRegistrations]

        public interface ICommand : IRequest;
        public interface IQuery : IRequest;

        public sealed record CreateUserCommand(string Name) : ICommand;
        public sealed record DeleteUserCommand(string Id) : ICommand;
        public sealed record GetUsersQuery : IQuery;
        public sealed record GetUserByIdQuery(string Id) : IQuery;

        public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, string>
        {
            public ValueTask<string> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("created");
        }

        public sealed class DeleteUserHandler : IRequestHandler<DeleteUserCommand, string>
        {
            public ValueTask<string> HandleAsync(DeleteUserCommand request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("deleted");
        }

        public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, string>
        {
            public ValueTask<string> HandleAsync(GetUsersQuery request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("users");
        }

        public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, string>
        {
            public ValueTask<string> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("user");
        }

        [Decorator(Order = 1)]
        public class CommandLoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : ICommand
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public CommandLoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 1)]
        public class QueryCachingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : IQuery
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public QueryCachingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 0)]
        public class GlobalDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public GlobalDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }
        """;

    // ── Scenario 4: Cross-assembly composition ──────────────────────────

    public const string CrossAssemblyHandlerLib = $$"""
        {{Usings}}

        [assembly: DecoratR.GenerateDecoratRMetadata]

        public interface ICommand : IRequest;

        public sealed record CreateTodoCommand(string Title) : ICommand;
        public sealed record GetTodosQuery : IRequest;

        public sealed class CreateTodoHandler : IRequestHandler<CreateTodoCommand, string>
        {
            public ValueTask<string> HandleAsync(CreateTodoCommand request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("created");
        }

        public sealed class GetTodosHandler : IRequestHandler<GetTodosQuery, string>
        {
            public ValueTask<string> HandleAsync(GetTodosQuery request, CancellationToken cancellationToken = default)
                => ValueTask.FromResult("todos");
        }

        [Decorator(Order = 1)]
        public class LibLoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            private readonly IRequestHandler<TRequest, TResponse> _inner;
            public LibLoggingDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => _inner.HandleAsync(request, cancellationToken);
        }
        """;

    public const string CrossAssemblyCompositionRoot = $$"""
        {{Usings}}

        [assembly: DecoratR.GenerateDecoratRRegistrations]
        """;
}
