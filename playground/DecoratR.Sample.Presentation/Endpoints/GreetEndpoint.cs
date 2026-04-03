using DecoratR.Sample.Application.Greetings.Commands;
using Microsoft.AspNetCore.Mvc;

namespace DecoratR.Sample.Presentation.Endpoints;

public static class GreetEndpoint
{
    public static IEndpointRouteBuilder MapGreetEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/greet", Handle);
        return builder;
    }

    private static async ValueTask<IResult> Handle(
        [FromServices] IRequestHandler<GreetCommand, string> handler,
        [FromBody] GreetRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(new GreetCommand(request.Name), cancellationToken);
        return Results.Ok(new Response(result));
    }
}

public sealed record GreetRequest(string Name);