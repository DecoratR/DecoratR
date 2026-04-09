using Examples.CleanArchitecture.Application.Abstractions;
using Examples.Shared;

namespace Examples.CleanArchitecture.Application.Todos.Queries;

public sealed record GetTodosQuery : IQuery;

internal sealed class GetTodosQueryHandler(ITodoRepository repository)
    : IQueryHandler<GetTodosQuery, IReadOnlyList<Todo>>
{
    public async ValueTask<IReadOnlyList<Todo>> HandleAsync(
        GetTodosQuery query,
        CancellationToken cancellationToken = default) =>
        await repository.GetAllAsync(cancellationToken);
}