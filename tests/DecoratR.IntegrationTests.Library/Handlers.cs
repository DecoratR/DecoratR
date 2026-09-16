using System.Runtime.CompilerServices;

namespace DecoratR.IntegrationTests.Library;

internal sealed class LibraryCommandHandler(ExecutionTrace trace) : IRequestHandler<LibraryCommand, string>
{
    public ValueTask<string> HandleAsync(LibraryCommand request, CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(LibraryCommandHandler));
        return ValueTask.FromResult($"Hello, {request.Name}");
    }
}

internal sealed class LibraryQueryHandler(ExecutionTrace trace) : IRequestHandler<LibraryQuery, int>
{
    public ValueTask<int> HandleAsync(LibraryQuery request, CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(LibraryQueryHandler));
        return ValueTask.FromResult(42);
    }
}

internal sealed class LibraryStreamHandler(ExecutionTrace trace) : IStreamRequestHandler<LibraryStream, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        LibraryStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        trace.Add(nameof(LibraryStreamHandler));
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}
