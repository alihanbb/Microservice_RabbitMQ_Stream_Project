namespace ShoppingCartService.Application.Commands.AddItem;

public record AddItemCommand(
    Guid UserId,
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price
);
