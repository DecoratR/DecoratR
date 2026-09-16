namespace Examples.ModularMonolith.Catalog.Domain;

public sealed class Product
{
    private Product(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Guid Id { get; }

    public string Name { get; }

    public decimal Price { get; }

    public static Product Create(string name, decimal price) => new(Guid.NewGuid(), name, price);

    public static Product Restore(Guid id, string name, decimal price) => new(id, name, price);
}
