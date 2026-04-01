namespace DecoratR.Sample.Application.Greetings.Commands;

public sealed record GreetCommand(string Name) : ICommand<string>;