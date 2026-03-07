namespace ShoppingCartService.Domain.Events;

public sealed record CartCreatedEvent(
    Guid CartId,
    Guid UserId,
    DateTime CreatedAt
) : DomainEvent, ICartEvent;

public sealed record ItemAddedToCartEvent(
    Guid CartId,
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price
) : DomainEvent, ICartEvent;

public sealed record ItemQuantityIncreasedEvent(
    Guid CartId,
    Guid ProductId,
    int AddedQuantity,
    int NewTotalQuantity
) : DomainEvent, ICartEvent;

public sealed record ItemRemovedFromCartEvent(
    Guid CartId,
    Guid ProductId
) : DomainEvent, ICartEvent;

public sealed record ItemQuantityUpdatedEvent(
    Guid CartId,
    Guid ProductId,
    int OldQuantity,
    int NewQuantity
) : DomainEvent, ICartEvent;

public sealed record CartConfirmedEvent(
    Guid CartId,
    decimal TotalAmount,
    int TotalItems,
    DateTime ConfirmedAt
) : DomainEvent, ICartEvent;

public sealed record CartClearedEvent(
    Guid CartId,
    int ItemsRemoved
) : DomainEvent, ICartEvent;
