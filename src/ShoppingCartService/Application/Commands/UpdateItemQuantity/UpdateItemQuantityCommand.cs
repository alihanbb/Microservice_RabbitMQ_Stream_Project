namespace ShoppingCartService.Application.Commands.UpdateItemQuantity;

public sealed record UpdateItemQuantityCommand(Guid UserId, Guid ProductId, int NewQuantity);
