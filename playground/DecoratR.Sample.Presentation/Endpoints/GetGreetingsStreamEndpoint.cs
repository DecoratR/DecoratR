using System.Runtime.CompilerServices;
using DecoratR.Sample.Application.Greetings.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DecoratR.Sample.Presentation.Endpoints;

public static class GetGreetingsStreamEndpoint
{
    public static IEndpointRouteBuilder MapGetGreetingsStreamEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/greetings/stream", Handle);
        return builder;
    }

    private static ServerSentEventsResult<string> Handle(
        [FromServices] IStreamRequestHandler<GetGreetingsStreamQuery, string> handler,
        [FromQuery] string? filter,
        CancellationToken cancellationToken)
    {

        return TypedResults.ServerSentEvents(handler.HandleAsync(new GetGreetingsStreamQuery(filter), cancellationToken));
    }
}
