using DecoratR.Sample.Application.Abstractions;

namespace DecoratR.Sample.Application.Greetings.Commands;

public sealed record GreetCommand(string Name) : ICommand;