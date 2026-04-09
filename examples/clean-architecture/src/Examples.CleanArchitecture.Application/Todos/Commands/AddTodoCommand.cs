using Examples.CleanArchitecture.Application.Abstractions;
using Examples.Shared;

namespace Examples.CleanArchitecture.Application.Todos.Commands;

public sealed record AddTodoCommand(string Title) : ICommand;

internal sealed class AddTodoCommandHandler(ITodoRepository repository)
    : ICommandHandler<AddTodoCommand, Todo>
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