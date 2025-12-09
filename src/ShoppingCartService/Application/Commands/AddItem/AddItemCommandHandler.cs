namespace ShoppingCartService.Application.Commands.AddItem;

public class AddItemCommandHandler
{
    private readonly ICartRepository _cartRepository;

    public AddItemCommandHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<Result<CartDto>> HandleAsync(AddItemCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await _cartRepository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
            {
                cart = Cart.Create(command.UserId);
            }

            if (cart.IsConfirmed)
            {
                return Result<CartDto>.Failure("Cannot add items to a confirmed cart", 400);
            }

            var cartItem = CartItem.Create(
                command.ProductId,
                command.ProductName,
                command.Category,
                command.Quantity,
                command.Price
            );

            cart.AddItem(cartItem);

            await _cartRepository.SaveAsync(cart, cancellationToken);

            return Result<CartDto>.Success(cart.ToDto(), 200);
        }
        catch (ArgumentException ex)
        {
            return Result<CartDto>.Failure(ex.Message, 400);
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
