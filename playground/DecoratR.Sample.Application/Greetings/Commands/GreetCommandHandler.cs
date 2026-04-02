using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Application.Greetings.Commands;

public sealed class GreetCommandHandler(IGreetingRepository repository)
    : IRequestHandler<GreetCommand, string>
{
    public async ValueTask<string> HandleAsync(GreetCommand command, CancellationToken cancellationToken = default)
    {
        var greeting = Greeting.Create(command.Name);
        await repository.AddAsync(greeting, cancellationToken);
        return greeting.Message;
    }
}