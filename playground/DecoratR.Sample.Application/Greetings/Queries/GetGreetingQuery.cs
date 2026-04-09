namespace DecoratR.Sample.Application.Greetings.Queries;

public sealed record GetGreetingQuery(string Name) : IRequest;