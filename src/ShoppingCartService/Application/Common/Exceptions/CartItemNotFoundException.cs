namespace ShoppingCartService.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested item is not found in the cart.
/// Maps to HTTP 404 Not Found.
/// </summary>
public sealed class CartItemNotFoundException(Guid productId)
    : CartDomainException($"Item with ProductId '{productId}' was not found in the cart", StatusCodes.Status404NotFound);
