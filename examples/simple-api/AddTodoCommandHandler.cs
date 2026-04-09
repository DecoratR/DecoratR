using DecoratR;
using Examples.Shared;

namespace Examples.SimpleApi;

internal sealed class AddTodoCommandHandler(ITodoRepository repository)
    : IRequestHandler<AddTodoCommand, Todo>
{
    public async ValueTask<Todo> HandleAsync(
        AddTodoCommand command,
        CancellationToken cancellationToken = default)
    {
        var todo = Todo.Create(command.Title);
        await repository.AddAsync(todo, cancellationToken);
        return todo;
    }
}
