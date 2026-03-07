using ShoppingCartService.Application.Common;
using ShoppingCartService.Application.DTOs;
using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Application.Mappers;

namespace ShoppingCartService.Application.Commands.ConfirmCart;

public sealed class ConfirmCartCommandHandlerV2(ICartAggregateRepository repository)
{
    public async Task<Result<CartDto>> HandleAsync(ConfirmCartCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
                return Result<CartDto>.Failure("Cart not found", 404);

            cart.Confirm();

            await repository.SaveAsync(cart, cancellationToken);

            return Result<CartDto>.Success(CartMapper.ToDto(cart));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CartDto>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"Failed to confirm cart: {ex.Message}", 500);
        }
    }
}
