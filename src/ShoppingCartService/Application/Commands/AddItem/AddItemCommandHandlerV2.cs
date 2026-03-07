using ShoppingCartService.Application.Common;
using ShoppingCartService.Application.DTOs;
using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Application.Mappers;
using ShoppingCartService.Domain.Aggregates;

namespace ShoppingCartService.Application.Commands.AddItem;

public sealed class AddItemCommandHandlerV2(ICartAggregateRepository repository)
{
    public async Task<Result<CartDto>> HandleAsync(AddItemCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
            {
                cart = CartAggregate.Create(command.UserId);
            }

            cart.AddItem(
                command.ProductId,
                command.ProductName,
                command.Category,
                command.Quantity,
                command.Price);

            await repository.SaveAsync(cart, cancellationToken);

            return Result<CartDto>.Success(CartMapper.ToDto(cart));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CartDto>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"Failed to add item: {ex.Message}", 500);
        }
    }
}
