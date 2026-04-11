using System.Runtime.CompilerServices;
using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Application.Greetings.Queries;

internal sealed class GetGreetingsStreamQueryHandler(IGreetingRepository repository)
    : IStreamQueryHandler<GetGreetingsStreamQuery, string>
{
    public async IAsyncEnumerable<string> HandleAsync(
        GetGreetingsStreamQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var greeting in repository.GetAllAsync(cancellationToken))
        {
            if (query.Filter is not null &&
                !greeting.Name.Contains(query.Filter, StringComparison.OrdinalIgnoreCase))
                continue;

            await Task.Delay(200, cancellationToken);
            yield return greeting.Message;
        }
    }
}
