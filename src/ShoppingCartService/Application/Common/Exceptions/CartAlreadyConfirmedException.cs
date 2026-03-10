namespace ShoppingCartService.Application.Common.Exceptions;

/// <summary>
/// Thrown when an attempt is made to modify a cart that has already been confirmed.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class CartAlreadyConfirmedException()
    : CartDomainException("Cannot modify a confirmed cart", StatusCodes.Status409Conflict);
