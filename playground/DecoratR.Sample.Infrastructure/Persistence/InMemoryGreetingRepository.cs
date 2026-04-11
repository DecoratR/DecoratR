using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Infrastructure.Persistence;

internal sealed class InMemoryGreetingRepository : IGreetingRepository
{
    private static readonly Greeting[] SeedData =
    [
        Greeting.Create("Alice"),
        Greeting.Create("Bob"),
        Greeting.Create("Charlie"),
        Greeting.Create("Diana"),
        Greeting.Create("Eve"),
    ];

    private readonly ConcurrentDictionary<string, Greeting> _greetings = new(
        SeedData.Select(g => new KeyValuePair<string, Greeting>(g.Name, g)),
        StringComparer.OrdinalIgnoreCase);

    public Task<Greeting?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _greetings.TryGetValue(name, out var greeting);
        return Task.FromResult(greeting);
    }

    public Task AddAsync(Greeting greeting, CancellationToken cancellationToken = default)
    {
        _greetings[greeting.Name] = greeting;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<Greeting> GetAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var greeting in _greetings.Values)
        {
            yield return greeting;
        }

        await Task.CompletedTask;
    }
}