using ShoppingCartService.Application.Common.Exceptions;
using ShoppingCartService.Application.Common.Handlers;

namespace ShoppingCartService.Application.Queries.GetCart;

public sealed class GetCartQueryHandler(
    ICartAggregateRepository repository,
    ILogger<GetCartQueryHandler> logger)
    : QueryHandlerBase<GetCartQuery, CartDto>(logger)
{
    protected override async Task<CartDto> ExecuteAsync(GetCartQuery query, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(query.UserId, cancellationToken)
                   ?? throw new CartNotFoundException();

        return CartMapper.ToDto(cart);
    }
}
