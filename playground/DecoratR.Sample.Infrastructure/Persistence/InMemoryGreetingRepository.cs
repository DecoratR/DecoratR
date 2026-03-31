using System.Collections.Concurrent;
using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Infrastructure.Persistence;

internal sealed class InMemoryGreetingRepository : IGreetingRepository
{
    private readonly ConcurrentDictionary<string, Greeting> _greetings = new(StringComparer.OrdinalIgnoreCase);

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
}
