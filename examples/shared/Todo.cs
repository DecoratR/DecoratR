namespace Examples.Shared;

public sealed class Todo
{
    private Todo(Guid id, string title, bool isCompleted, DateTime createdAt)
    {
        Id = id;
        Title = title;
        IsCompleted = isCompleted;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public bool IsCompleted { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static Todo Create(string title) =>
        new(Guid.NewGuid(), title, false, DateTime.UtcNow);

    internal static Todo Create(Guid id, string title, bool isCompleted, DateTime createdAt) =>
        new(id, title, isCompleted, createdAt);
}