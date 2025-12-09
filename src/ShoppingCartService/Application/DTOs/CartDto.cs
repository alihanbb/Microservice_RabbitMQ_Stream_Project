namespace ShoppingCartService.Application.DTOs;

public record CartDto(
    Guid Id,
    Guid UserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsConfirmed,
    decimal TotalAmount,
    IEnumerable<CartItemDto> Items
);

public record CartItemDto(
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price,
    decimal TotalPrice
);
