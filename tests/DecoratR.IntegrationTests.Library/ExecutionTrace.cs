namespace DecoratR.IntegrationTests.Library;

/// <summary>Records the order in which decorators and handlers run.</summary>
public sealed class ExecutionTrace
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry)
    {
        lock (_entries) _entries.Add(entry);
    }

    public void Clear()
    {
        lock (_entries) _entries.Clear();
    }
}
