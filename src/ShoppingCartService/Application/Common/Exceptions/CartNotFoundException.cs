namespace ShoppingCartService.Application.Common.Exceptions;

public class CartNotFoundException : CartDomainException
{
    public CartNotFoundException(string message = "Cart not found") : base(message, 404) { }
}
