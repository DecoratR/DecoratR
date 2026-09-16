using DecoratR;
using Examples.ModularMonolith.SharedKernel.Messaging;

namespace Examples.ModularMonolith.Catalog.Contracts;

// The public surface of the Catalog module: requests and DTOs. Handlers stay internal to the module.

/// <summary>Marker for commands owned by the Catalog module; the module applies its own decorators to them.</summary>
public interface ICatalogCommand : ICommand;

public sealed record ProductDto(Guid Id, string Name, decimal Price);

public sealed record CreateProductCommand(string Name, decimal Price) : ICatalogCommand;

public sealed record GetProductQuery(Guid Id) : IQuery<ProductDto?>;

public sealed record ListProductsQuery : IQuery<IReadOnlyList<ProductDto>>;

public sealed record StreamProductsQuery : IStreamRequest;
