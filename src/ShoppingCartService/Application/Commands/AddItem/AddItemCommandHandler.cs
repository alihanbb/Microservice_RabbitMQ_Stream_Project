
using ShoppingCartService.Domain.Aggregates;

namespace ShoppingCartService.Application.Commands.AddItem;

public sealed class AddItemCommandHandler(
    ICartAggregateRepository repository,
    ILogger<AddItemCommandHandler> logger)
    : CommandHandlerBase<AddItemCommand, CartDto>(logger)
{
    protected override async Task<CartDto> ExecuteAsync(AddItemCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken)
                   ?? CartAggregate.Create(command.UserId);

        cart.AddItem(
            command.ProductId,
            command.ProductName,
            command.Category,
            command.Quantity,
            command.Price);

        await repository.SaveAsync(cart, cancellationToken);

        return CartMapper.ToDto(cart);
    }
}
