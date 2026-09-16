namespace Examples.ModularMonolith.Orders.Domain;

public sealed class Order
{
    private Order(Guid id, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        Id = id;
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        Total = unitPrice * quantity;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public string ProductName { get; }

    public int Quantity { get; }

    public decimal Total { get; }

    public static Order Place(Guid productId, string productName, int quantity, decimal unitPrice) =>
        new(Guid.NewGuid(), productId, productName, quantity, unitPrice);
}
