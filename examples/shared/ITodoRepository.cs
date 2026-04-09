namespace Examples.Shared;

public interface ITodoRepository
{
    ValueTask<IReadOnlyList<Todo>> GetAllAsync(CancellationToken cancellationToken = default);
    ValueTask AddAsync(Todo todo, CancellationToken cancellationToken = default);
}
