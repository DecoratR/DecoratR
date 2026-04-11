using DecoratR.Sample.Application.Abstractions;

namespace DecoratR.Sample.Application.Greetings.Queries;

public sealed record GetGreetingsStreamQuery(string? Filter = null) : IStreamQuery;
