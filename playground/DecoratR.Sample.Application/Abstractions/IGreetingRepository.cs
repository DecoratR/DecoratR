using DecoratR.Sample.Domain;

namespace DecoratR.Sample.Application.Abstractions;

public interface IGreetingRepository
{
    Task<Greeting?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task AddAsync(Greeting greeting, CancellationToken cancellationToken);
}