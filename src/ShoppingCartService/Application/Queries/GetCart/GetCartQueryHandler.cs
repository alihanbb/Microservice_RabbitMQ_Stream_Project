namespace ShoppingCartService.Application.Queries.GetCart;

public class GetCartQueryHandler
{
    private readonly ICartRepository _cartRepository;

    public GetCartQueryHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<Result<CartDto>> HandleAsync(GetCartQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await _cartRepository.GetByUserIdAsync(query.UserId, cancellationToken);

            if (cart == null)
            {
                return Result<CartDto>.Failure("Cart not found", 404);
            }

            return Result<CartDto>.Success(cart.ToDto(), 200);
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"An error occurred: {ex.Message}", 500);
        }
    }
}
