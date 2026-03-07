namespace ShoppingCartService.Application.Queries.GetCart;

public sealed class GetCartQueryHandlerV2(ICartAggregateRepository repository)
{
    public async Task<Result<CartDto>> HandleAsync(GetCartQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await repository.GetByUserIdAsync(query.UserId, cancellationToken);

            if (cart == null)
                return Result<CartDto>.Failure("Cart not found", 404);

            return Result<CartDto>.Success(CartMapper.ToDto(cart));
        }
        catch (Exception ex)
        {
            return Result<CartDto>.Failure($"Failed to get cart: {ex.Message}", 500);
        }
    }
}
