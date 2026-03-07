using ShoppingCartService.Application.Common;
using ShoppingCartService.Application.DTOs;
using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Application.Mappers;

namespace ShoppingCartService.Application.Commands.RemoveItem;

public sealed class RemoveItemCommandHandlerV2(ICartAggregateRepository repository)
{
    public async Task<Result<CartDto>> HandleAsync(RemoveItemCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
                return Result<CartDto>.Failure("Cart not found", 404);

            cart.RemoveItem(command.ProductId);

            await repository.SaveAsync(cart, cancellationToken);

            return Result<CartDto>.Success(CartMapper.ToDto(cart));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CartDto>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"Failed to remove item: {ex.Message}", 500);
        }
    }
}
