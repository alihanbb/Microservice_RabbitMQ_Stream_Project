using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Application.Commands.ConfirmCart;

public class ConfirmCartCommandHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IRabbitMQStreamPublisher _streamPublisher;
    private readonly ILogger<ConfirmCartCommandHandler> _logger;

    public ConfirmCartCommandHandler(
        ICartRepository cartRepository, 
        IRabbitMQStreamPublisher streamPublisher,
        ILogger<ConfirmCartCommandHandler> logger)
    {
        _cartRepository = cartRepository;
        _streamPublisher = streamPublisher;
        _logger = logger;
    }

    public async Task<Result<CartDto>> HandleAsync(ConfirmCartCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var cart = await _cartRepository.GetByUserIdAsync(command.UserId, cancellationToken);

            if (cart == null)
            {
                _logger.LogWarning("Cart not found for user {UserId}", command.UserId);
                return Result<CartDto>.Failure("Cart not found", 404);
            }

            cart.Confirm();

            // Save confirmed cart to repository
            await _cartRepository.SaveAsync(cart, cancellationToken);

            // Publish CartConfirmedEvent to RabbitMQ Stream with error handling
            var cartDto = cart.ToDto();
            var confirmedEvent = new CartConfirmedEvent(
                CartId: cart.Id,
                TotalAmount: cartDto.TotalPrice,
                TotalItems: cartDto.Items.Count,
                ConfirmedAt: DateTime.UtcNow);

            try
            {
                await _streamPublisher.PublishAsync(confirmedEvent, cancellationToken);
                _logger.LogInformation("Cart {CartId} confirmed and event published", cart.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish CartConfirmedEvent for cart {CartId}. Cart is confirmed but event was not published.", cart.Id);
                // Log the error but return success since cart is already confirmed in repository
                // The event will need to be manually retried or a separate event log/outbox pattern should be implemented
                // For now, we continue and return the confirmed cart
            }

            return Result<CartDto>.Success(cartDto, 200);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while confirming cart for user {UserId}", command.UserId);
            return Result<CartDto>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error confirming cart for user {UserId}", command.UserId);
            return Result<CartDto>.Failure($"An error occurred: {ex.Message}", 500);
        }
    }
}

