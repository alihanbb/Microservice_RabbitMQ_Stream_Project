namespace ShoppingCartService.Application.Commands.RemoveItem;

public class RemoveItemCommandHandler
{
    private readonly ICartRepository _cartRepository;

    public RemoveItemCommandHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<Result<CartDto>> HandleAsync(RemoveItemCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await _cartRepository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
            {
                return Result<CartDto>.Failure("Cart not found", 404);
            }

            if (cart.IsConfirmed)
            {
                return Result<CartDto>.Failure("Cannot remove items from a confirmed cart", 400);
            }

            cart.RemoveItem(command.ProductId);

            await _cartRepository.SaveAsync(cart, cancellationToken);

            return Result<CartDto>.Success(cart.ToDto(), 200);
        }
        catch (InvalidOperationException ex)
        {
            return Result<CartDto>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"An error occurred: {ex.Message}", 500);
        }
    }
}
