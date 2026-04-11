using DecoratR.Sample.Application.Greetings.Queries;
using Microsoft.AspNetCore.Mvc;

namespace DecoratR.Sample.Presentation.Endpoints;

public static class GetGreetingEndpoint
{
    public static IEndpointRouteBuilder MapGetGreetingEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/greeting/{name}", Handle);
        return builder;
    }

    private static async ValueTask<IResult> Handle(
        [FromServices] IRequestHandler<GetGreetingQuery, string> handler,
        [FromRoute] string name,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetGreetingQuery(name), cancellationToken);
        return Results.Ok(new Response(result));
    }
}