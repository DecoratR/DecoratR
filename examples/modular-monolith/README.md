# Modular Monolith Example

A modular monolith with two modules (**Catalog** and **Orders**), a **shared kernel** and one executable
(**Api**) that acts as the composition root. Every module follows Clean Architecture:

```
examples/modular-monolith/
├── Examples.ModularMonolith.Api/                composition root: [GenerateDecoratRRegistrations], AddDecoratR()
├── SharedKernel/…SharedKernel/                  ICommand, IQuery<T>, IValidator<T>, cross-cutting decorators
└── Modules/
    ├── Catalog/
    │   ├── …Catalog.Contracts/                  public requests + DTOs (the module's API for other modules)
    │   ├── …Catalog.Domain/                     entities, repository interfaces
    │   ├── …Catalog.Application/                internal handlers, validators, module-scoped decorator
    │   ├── …Catalog.Infrastructure/             repositories, AddCatalogModule()
    │   └── …Catalog.Presentation/               minimal API endpoints, MapCatalogEndpoints()
    └── Orders/                                  same layout; Orders.Application references Catalog.Contracts only
```

## What it shows

- **One `AddDecoratR()` for the whole monolith.** Every `Application` project and the shared kernel carry
  `[assembly: GenerateDecoratRMetadata]`; the Api carries `[assembly: GenerateDecoratRRegistrations]` and
  generates the complete registration for all of them. Handlers and decorators stay `internal` to their module.
- **Modules talk through contracts.** `PlaceOrderCommandHandler` (Orders) injects
  `IRequestHandler<GetProductQuery, ProductDto?>` from `Catalog.Contracts` and gets Catalog's fully decorated
  pipeline, without referencing `Catalog.Application`.
- **Cross-cutting decorators in the shared kernel.** `LoggingDecorator` (Order 0, every request),
  `QueryTimingDecorator` (Order 1, `where TRequest : IQuery<TResponse>`), `ValidationDecorator` (Order 10,
  `where TRequest : ICommand`, runs every registered `IValidator<TRequest>`).
- **Module-scoped decorators.** `CatalogAuditDecorator` (Order 5) is constrained to `ICatalogCommand` and wraps
  Catalog commands only, wherever they are dispatched from.
- **Stream pipeline.** `GET /catalog/products/stream` resolves an `IStreamRequestHandler`.

## Run

```bash
dotnet run --project examples/modular-monolith/Examples.ModularMonolith.Api
```

```bash
curl http://localhost:5000/catalog/products
curl -X POST http://localhost:5000/catalog/products -H 'Content-Type: application/json' -d '{"name":"Monitor","price":199.0}'
curl -X POST http://localhost:5000/orders -H 'Content-Type: application/json' -d '{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}'
curl -X POST http://localhost:5000/orders -H 'Content-Type: application/json' -d '{"productId":"11111111-1111-1111-1111-111111111111","quantity":0}'   # 400 from the validation decorator
curl http://localhost:5000/catalog/products/stream
```

The generated code can be inspected under each project's `obj/…/generated/DecoratR.Generator/` folder.

> This example references the DecoratR projects in `src/` directly so it always exercises the current
> generator. In your own solution, reference the `DecoratR.Abstractions` and `DecoratR.Generator` packages instead.
