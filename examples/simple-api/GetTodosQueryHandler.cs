using DecoratR;
using Examples.Shared;

namespace Examples.SimpleApi;

internal sealed class GetTodosQueryHandler(ITodoRepository repository)
    : IRequestHandler<GetTodosQuery, IReadOnlyList<Todo>>
{
    public async ValueTask<IReadOnlyList<Todo>> HandleAsync(
        GetTodosQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}