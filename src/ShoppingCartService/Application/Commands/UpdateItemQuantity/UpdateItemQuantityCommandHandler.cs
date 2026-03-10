using ShoppingCartService.Application.Common.Exceptions;
using ShoppingCartService.Application.Common.Handlers;

namespace ShoppingCartService.Application.Commands.UpdateItemQuantity;

public sealed class UpdateItemQuantityCommandHandler(
    ICartAggregateRepository repository,
    ILogger<UpdateItemQuantityCommandHandler> logger)
    : CommandHandlerBase<UpdateItemQuantityCommand, CartDto>(logger)
{
    protected override async Task<CartDto> ExecuteAsync(UpdateItemQuantityCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken)
                   ?? throw new CartNotFoundException();

        cart.UpdateItemQuantity(command.ProductId, command.NewQuantity);

        await repository.SaveAsync(cart, cancellationToken);

        return CartMapper.ToDto(cart);
    }
}
