namespace ShoppingCartService.Application.Events;

/// <summary>
/// Integration event published to RabbitMQ Super Stream when a cart is confirmed.
/// Consumers (DiscountService, NotificationService) subscribe to this event.
/// Richer than the domain CartConfirmedEvent — includes UserId and item snapshot.
/// </summary>
public record CartConfirmedIntegrationEvent(
    Guid CartId,
    Guid UserId,
    decimal TotalAmount,
    int TotalItems,
    IReadOnlyCollection<CartItemSnapshot> Items,
    DateTime ConfirmedAt);

/// <summary>
/// Immutable snapshot of a cart item at the time of confirmation.
/// </summary>
public record CartItemSnapshot(
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price);
