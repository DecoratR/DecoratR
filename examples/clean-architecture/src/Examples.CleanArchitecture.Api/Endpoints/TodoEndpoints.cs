namespace Examples.CleanArchitecture.Api.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this WebApplication app)
    {
        app.MapGetTodosEndpoint();
        app.MapAddTodoEndpoint();
    }
}
