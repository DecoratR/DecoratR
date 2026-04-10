namespace Examples.Shared;

public interface ITodoRepository
{
    ValueTask<IReadOnlyList<Todo>> GetAllAsync(CancellationToken cancellationToken);

    ValueTask AddAsync(Todo todo, CancellationToken cancellationToken);
}