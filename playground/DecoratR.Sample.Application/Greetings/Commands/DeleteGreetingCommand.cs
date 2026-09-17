using DecoratR.Sample.Application.Abstractions;

namespace DecoratR.Sample.Application.Greetings.Commands;

public sealed record DeleteGreetingCommand(string Name) : ICommand;
