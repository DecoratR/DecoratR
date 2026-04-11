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

    // ── Decorators ──────────────────────────────────────────────────────

    public static string Decorator(string name, int order, string constraint = "IRequest") => $$"""
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

    public static string InternalDecorator(string name, int order, string constraint = "IRequest") => $$"""
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

    // ── Composite source builders ───────────────────────────────────────

    public static string HandlerOnly(string body = "") =>
        $"""
         using DecoratR;

         {MetadataAttribute}

         {TestCommandRecord}

         {TestCommandHandler}
         {body}
         """;

    public static string FullPath(string body = "") =>
        $"""
         using DecoratR;

         {RegistrationsAttribute}

         {TestCommandRecord}

         {TestCommandHandler}
         {body}
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
}
