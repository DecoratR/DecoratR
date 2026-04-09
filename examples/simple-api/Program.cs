using DecoratR;
using Examples.Shared;
using Examples.SimpleApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
builder.Services.AddSharedInfrastructure();

var app = builder.Build();

app.MapGet("/todos", async (IRequestHandler<GetTodosQuery, IReadOnlyList<Todo>> handler, CancellationToken ct) =>
{
    var todos = await handler.HandleAsync(new GetTodosQuery(), ct);
    return Results.Ok(todos);
});

app.MapPost("/todos", async (IRequestHandler<AddTodoCommand, Todo> handler, AddTodoRequest request, CancellationToken ct) =>
{
    var todo = await handler.HandleAsync(new AddTodoCommand(request.Title), ct);
    return Results.Created($"/todos/{todo.Id}", todo);
});

app.Run();

public sealed record AddTodoRequest(string Title);
