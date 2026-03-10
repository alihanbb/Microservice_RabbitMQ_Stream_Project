using ShoppingCartService.Application.Common.Exceptions;
using ShoppingCartService.Application.Common.Handlers;
using ShoppingCartService.Application.Events;

namespace ShoppingCartService.Application.Commands.ConfirmCart;

public sealed class ConfirmCartCommandHandler(
    ICartAggregateRepository repository,
    IRabbitMQStreamPublisher streamPublisher,
    ILogger<ConfirmCartCommandHandler> logger)
    : CommandHandlerBase<ConfirmCartCommand, CartDto>(logger)
{
    protected override async Task<CartDto> ExecuteAsync(ConfirmCartCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(command.UserId, cancellationToken)
                   ?? throw new CartNotFoundException();

        cart.Confirm();

        await repository.SaveAsync(cart, cancellationToken);

        var integrationEvent = new CartConfirmedIntegrationEvent(
            CartId: cart.Id,
            UserId: cart.UserId,
            TotalAmount: cart.GetTotalAmount(),
            TotalItems: cart.GetTotalItems(),
            Items: cart.Items.Select(i => new CartItemSnapshot(
                i.ProductId, i.ProductName, i.Category, i.Quantity, i.Price)).ToList(),
            ConfirmedAt: DateTime.UtcNow);

        // Publisher hatası cart'ın confirmed durumunu etkilemez (fire-and-forget with logging)
        try
        {
            await streamPublisher.PublishAsync(integrationEvent, cart.Id.ToString(), cancellationToken);
            Logger.LogInformation("Cart {CartId} confirmed and integration event published", cart.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to publish CartConfirmedIntegrationEvent for cart {CartId}. Cart is confirmed but event was not published.", cart.Id);
        }

        return CartMapper.ToDto(cart);
    }
}
