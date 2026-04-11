using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Application.Greetings.Queries;

internal sealed class GetGreetingQueryHandler(IGreetingRepository repository)
    : IQueryHandler<GetGreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(GetGreetingQuery query, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByNameAsync(query.Name, cancellationToken);

        if (existing is not null) return existing.Message;

        var greeting = Greeting.Create(query.Name);
        return greeting.Message;
    }
}