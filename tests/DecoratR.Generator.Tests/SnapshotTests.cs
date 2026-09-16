using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

/// <summary>
/// Full-output snapshots of the generated files. They guard the shape of the emitted code against accidental
/// changes; update them deliberately with <c>UPDATE_SNAPSHOTS=1 dotnet test</c>.
/// </summary>
public class SnapshotTests : GeneratorTestBase
{
    private const string LibrarySource = """
        using DecoratR;

        [assembly: DecoratR.GenerateDecoratRMetadata]

        namespace Sample.Library;

        public interface ICommand : IRequest;

        public sealed record CreateUser(string Name) : ICommand;
        public sealed record GetUsers : IRequest;
        public sealed record StreamUsers : IStreamRequest;

        public sealed class CreateUserHandler : IRequestHandler<CreateUser, int>
        {
            public ValueTask<int> HandleAsync(CreateUser request, CancellationToken cancellationToken = default) => default;
        }

        internal sealed class GetUsersHandler : IRequestHandler<GetUsers, IReadOnlyList<string>>
        {
            public ValueTask<IReadOnlyList<string>> HandleAsync(GetUsers request, CancellationToken cancellationToken = default) => default;
        }

        public sealed class StreamUsersHandler : IStreamRequestHandler<StreamUsers, string>
        {
            public async IAsyncEnumerable<string> HandleAsync(StreamUsers request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.Yield();
                yield return "user";
            }
        }

        [Decorator(Order = 1)]
        public sealed class LoggingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 2)]
        internal sealed class CommandDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
            where TRequest : class, ICommand
            where TResponse : struct
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
        }

        [Decorator(Order = 1)]
        public sealed class StreamLoggingDecorator<TRequest, TResponse>(IStreamRequestHandler<TRequest, TResponse> inner) : IStreamRequestHandler<TRequest, TResponse>
            where TRequest : IStreamRequest
        {
            public IAsyncEnumerable<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
        }
        """;

    private const string HostSource = """
        using DecoratR;

        [assembly: DecoratR.GenerateDecoratRRegistrations]

        namespace Sample.Host;

        public sealed record Ping : IRequest;

        public sealed class PingHandler : IRequestHandler<Ping, string>
        {
            public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken = default) => default;
        }

        [Decorator(Order = 0)]
        public sealed class TimingDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
            where TRequest : IRequest
        {
            public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) => inner.HandleAsync(request, cancellationToken);
        }
        """;

    [Fact]
    public void Library_HandlerRegistry()
    {
        var result = RunGenerator(LibrarySource, "Sample.Library").ShouldCompile();

        Snapshot.Verify("Library_HandlerRegistry", result.HandlerRegistry);
    }

    [Fact]
    public void Library_DecoratorRegistry()
    {
        var result = RunGenerator(LibrarySource, "Sample.Library").ShouldCompile();

        Snapshot.Verify("Library_DecoratorRegistry", result.DecoratorRegistry);
    }

    [Fact]
    public void Host_SingleAssembly()
    {
        var result = RunGenerator(TestSources.MixedFullPath(
            TestSources.Decorator("LoggingDecorator", 1) + "\n" +
            TestSources.Decorator("ValidationDecorator", 2) + "\n" +
            TestSources.StreamDecorator("StreamLoggingDecorator", 1)), "Sample.App").ShouldCompile();

        Snapshot.Verify("Host_SingleAssembly", result.Registrations);
    }

    [Fact]
    public void Host_CrossAssembly()
    {
        var (_, host) = RunTwoStageGenerator(LibrarySource, HostSource, "Sample.Library", "Sample.Host");
        host.ShouldCompile();

        Snapshot.Verify("Host_CrossAssembly", host.Registrations);
    }
}
