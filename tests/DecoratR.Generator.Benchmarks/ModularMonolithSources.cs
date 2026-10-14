namespace DecoratR.Generator.Benchmarks;

/// <summary>
/// Sources for a modular monolith: a shared kernel with cross-cutting decorators and modules with one file per
/// request/handler pair (like real projects, so an edit invalidates one syntax tree only).
/// </summary>
internal static class ModularMonolithSources
{
    public const string SharedKernelAssemblyName = "SharedKernel";
    public const string HostAssemblyName = "Host";
    public const string HostPath = "Host/Program.cs";

    public const string SharedKernel = """
        using DecoratR;

        [assembly: DecoratR.GenerateDecoratRMetadata]

        namespace SharedKernel;

        public interface ICommand : IRequest;

        public interface IQuery<TResult> : IRequest;

        [Decorator(Order = 0)]
        internal sealed class LoggingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner)
            : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 1)]
        internal sealed class QueryTimingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner)
            : IRequestHandler<TRequest, TResponse>
            where TRequest : IQuery<TResponse>
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 10)]
        internal sealed class ValidationDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner)
            : IRequestHandler<TRequest, TResponse>
            where TRequest : ICommand
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                => inner.HandleAsync(request, cancellationToken);
        }
        """;

    public const string Host = """
        [assembly: DecoratR.GenerateDecoratRRegistrations]

        namespace Host;

        public static class Program
        {
            public static int Value => 1;
        }
        """;

    public static string ModuleAssemblyName(int module) => "Module" + module;

    /// <summary>Path of the file that declares handler <paramref name="handler"/> of a module.</summary>
    public static string HandlerPath(int module, int handler) => $"Module{module}/Handler{handler}.cs";

    /// <summary>
    /// One module: a module marker with a module-scoped audit decorator, and <paramref name="handlers"/> files that
    /// alternate between commands (<c>Guid</c> response) and queries (<c>IReadOnlyList&lt;Dto&gt;</c> response).
    /// </summary>
    public static IEnumerable<(string Path, string Source)> Module(int module, int handlers)
    {
        yield return ($"Module{module}/Module.cs", $$"""
            using DecoratR;
            using SharedKernel;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            namespace Module{{module}};

            public interface IModule{{module}}Command : ICommand;

            [Decorator(Order = 5)]
            internal sealed class AuditDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner)
                : IRequestHandler<TRequest, TResponse>
                where TRequest : IModule{{module}}Command
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """);

        for (var i = 0; i < handlers; i++)
        {
            var source = i % 2 == 0
                ? $$"""
                    using DecoratR;

                    namespace Module{{module}};

                    public sealed record Command{{i}}(string Name) : IModule{{module}}Command;

                    internal sealed class Command{{i}}Handler : IRequestHandler<Command{{i}}, Guid>
                    {
                        public ValueTask<Guid> HandleAsync(Command{{i}} request, CancellationToken cancellationToken = default)
                            => ValueTask.FromResult(Guid.Empty);
                    }
                    """
                : $$"""
                    using DecoratR;
                    using SharedKernel;

                    namespace Module{{module}};

                    public sealed record Dto{{i}}(Guid Id, string Name);

                    public sealed record Query{{i}}(Guid Id) : IQuery<IReadOnlyList<Dto{{i}}>>;

                    internal sealed class Query{{i}}Handler : IRequestHandler<Query{{i}}, IReadOnlyList<Dto{{i}}>>
                    {
                        public ValueTask<IReadOnlyList<Dto{{i}}>> HandleAsync(Query{{i}} request, CancellationToken cancellationToken = default)
                            => ValueTask.FromResult<IReadOnlyList<Dto{{i}}>>([]);
                    }
                    """;

            yield return (HandlerPath(module, i), source);
        }
    }
}
