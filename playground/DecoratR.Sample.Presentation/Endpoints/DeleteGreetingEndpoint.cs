using DecoratR.Sample.Application.Greetings.Commands;
using Microsoft.AspNetCore.Mvc;

namespace DecoratR.Sample.Presentation.Endpoints;

public static class DeleteGreetingEndpoint
{
    public static IEndpointRouteBuilder MapDeleteGreetingEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/greetings/{name}", Handle);
        return builder;
    }

    private static async ValueTask<IResult> Handle(
        [FromServices] IRequestHandler<DeleteGreetingCommand> handler,
        string name,
        CancellationToken cancellationToken = default)
    {
        await handler.HandleAsync(new DeleteGreetingCommand(name), cancellationToken);
        return Results.NoContent();
    }
}
