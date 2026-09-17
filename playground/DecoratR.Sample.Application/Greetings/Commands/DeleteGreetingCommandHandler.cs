using DecoratR.Sample.Application.Abstractions;

namespace DecoratR.Sample.Application.Greetings.Commands;

/// <summary>A command handler without a response: it returns a plain <see cref="ValueTask"/>.</summary>
internal sealed class DeleteGreetingCommandHandler(IGreetingRepository repository)
    : ICommandHandler<DeleteGreetingCommand>
{
    public async ValueTask HandleAsync(DeleteGreetingCommand command, CancellationToken cancellationToken = default)
    {
        await repository.RemoveAsync(command.Name, cancellationToken);
    }
}
