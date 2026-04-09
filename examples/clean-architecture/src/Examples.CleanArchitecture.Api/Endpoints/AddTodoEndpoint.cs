using DecoratR;
using Examples.CleanArchitecture.Application.Todos.Commands;
using Examples.Shared;

namespace Examples.CleanArchitecture.Api.Endpoints;

public static class AddTodoEndpoint
{
    public static void MapAddTodoEndpoint(this WebApplication app)
    {
        app.MapPost("/todos", async (
            IRequestHandler<AddTodoCommand, Todo> handler,
            AddTodoRequest request,
            CancellationToken ct) =>
        {
            var todo = await handler.HandleAsync(new AddTodoCommand(request.Title), ct);
            return Results.Created($"/todos/{todo.Id}", todo);
        });
    }
}

public sealed record AddTodoRequest(string Title);
