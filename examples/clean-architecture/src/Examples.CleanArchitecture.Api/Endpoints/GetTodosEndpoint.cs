using DecoratR;
using Examples.CleanArchitecture.Application.Todos.Queries;
using Examples.Shared;

namespace Examples.CleanArchitecture.Api.Endpoints;

public static class GetTodosEndpoint
{
    public static void MapGetTodosEndpoint(this WebApplication app)
    {
        app.MapGet("/todos", async (
            IRequestHandler<GetTodosQuery, IReadOnlyList<Todo>> handler,
            CancellationToken ct) =>
        {
            var todos = await handler.HandleAsync(new GetTodosQuery(), ct);
            return Results.Ok(todos);
        });
    }
}
