namespace ShoppingCartService.Application.Commands.ConfirmCart;

public class ConfirmCartCommandHandler
{
    private readonly ICartRepository _cartRepository;

    public ConfirmCartCommandHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<Result<CartDto>> HandleAsync(ConfirmCartCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await _cartRepository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
            {
                return Result<CartDto>.Failure("Cart not found", 404);
            }

            cart.Confirm();

            await _cartRepository.SaveAsync(cart, cancellationToken);

            // TODO: Publish CartConfirmedEvent to RabbitMQ Stream for other services

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
