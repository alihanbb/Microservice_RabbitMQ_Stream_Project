using ShoppingCartService.Application.Common.Exceptions;
using ShoppingCartService.Application.Common.Handlers;

namespace ShoppingCartService.Application.Commands.RemoveItem;

public sealed class RemoveItemCommandHandler(
    ICartAggregateRepository repository,
    ILogger<RemoveItemCommandHandler> logger)
    : CommandHandlerBase<RemoveItemCommand, CartDto>(logger)
{
    protected override async Task<CartDto> ExecuteAsync(RemoveItemCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken)
                   ?? throw new CartNotFoundException();

        cart.RemoveItem(command.ProductId);

        await repository.SaveAsync(cart, cancellationToken);

        return CartMapper.ToDto(cart);
    }
}
