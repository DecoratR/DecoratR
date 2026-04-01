namespace DecoratR.Sample.Domain;

public sealed class Greeting
{
    private Greeting(Guid id, string name, string message, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Message = message;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Message { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static Greeting Create(string name) =>
        new(
            Guid.NewGuid(),
            name,
            $"Hello, {name}!",
            DateTime.UtcNow);
}