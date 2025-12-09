namespace ShoppingCartService.Application.Commands.RemoveItem;

public record RemoveItemCommand(
    Guid UserId,
    Guid ProductId
);
