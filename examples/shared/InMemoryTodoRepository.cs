using System.Collections.Concurrent;
using Bogus;

namespace Examples.Shared;

public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, Todo> _todos = new();

    public InMemoryTodoRepository()
    {
        var faker = new Faker<Todo>()
            .CustomInstantiator(f => Todo.Create(
                f.Random.Guid(),
                f.Lorem.Sentence(3),
                f.Random.Bool(),
                f.Date.Recent(30).ToUniversalTime()));

        foreach (var todo in faker.Generate(10))
        {
            _todos[todo.Id] = todo;
        }
    }

    public ValueTask<IReadOnlyList<Todo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Todo> todos = [.. _todos.Values.OrderByDescending(t => t.CreatedAt)];
        return ValueTask.FromResult(todos);
    }

    public ValueTask AddAsync(Todo todo, CancellationToken cancellationToken = default)
    {
        _todos[todo.Id] = todo;
        return ValueTask.CompletedTask;
    }
}